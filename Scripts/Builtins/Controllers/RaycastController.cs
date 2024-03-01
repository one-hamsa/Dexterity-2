using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using Debug = UnityEngine.Debug;

namespace OneHamsa.Dexterity.Builtins
{
    public class RaycastController : MonoBehaviour, IRaycastController
    {
        
        public class PressAnywhereEvent
        {
            public RaycastController controller;
            public bool propagate;

            public void StopPropagation()
            {
                propagate = false;
            }
            
            /// <summary>
            /// simulate a press event on this controller.
            /// can be used to trigger a delayed press from code.
            /// </summary>
            public void SimulateControllerPress()
            {
                controller.HandlePressed();
            }
        }
        public delegate void PressAnywhereHandler(PressAnywhereEvent e);
        
        const float rayLength = 100f;
        const int maxHits = 20;

        // Raycast Receiver filter
        private static readonly List<RaycastFilter> filters = new();
        public static event PressAnywhereHandler onAnyPress;

        public LayerMask layerMask = int.MaxValue;
        [Tooltip("How far back should the ray be casted from this transform?")]
        public float backRayLength = .25f;

        public RaycastController[] mutuallyExclusiveControllers;
        public bool defaultController;

        [Header("Debug")] public Color debugColliderColor = new Color(1f, .5f, 0f);
        public Color debugHitColor = new Color(1f, .25f, 0f);

        public bool current =>
            enabled && ((lastControllerPressed == null && defaultController) || lastControllerPressed == this);

        public Ray ray { get; private set; }
        public Ray displayRay { get; private set; }
        public bool didHit { get; private set; }
        public RaycastHit hit { get; private set; }
        private int pressStartFrame = -1;
        private RaycastController lastControllerPressed;

        private readonly RaycastHit[] hits = new RaycastHit[maxHits];
        private readonly List<IRaycastReceiver> lastReceivers = new(4);
        private IRaycastController.RaycastEvent.Result lastEventResult;

        [Preserve]
        public IRaycastController.RaycastEvent.Result GetLastEventResult() => lastEventResult;

        public delegate bool RaycastFilter(IRaycastReceiver receiver);

        private Transform _transform;

        private static readonly Comparer<RaycastHit> raycastDistanceComparer
            = Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));
        
        protected virtual void Awake()
        {
            _transform = transform;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }
        
        protected virtual void OnDestroy()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        private void OnSceneUnloaded(Scene scene)
        {
            using (ListPool<IRaycastReceiver>.Get(out var receivers))
            {
                receivers.AddRange(lastReceivers);
                foreach (var receiver in receivers)
                {
                    if (receiver is MonoBehaviour mono && mono == null)
                    {
                        lastReceivers.Remove(receiver);
                    }
                }
            }
        }

        /// <summary>
        /// Set Global Raycast Receiver filter
        /// </summary>
        public static RaycastFilter AddFilter(RaycastFilter filter)
        {
            filters.Add(filter);
            return filter;
        }

        public static RaycastFilter AddFilter(Transform root, RaycastFilter orFilter = null)
        {
            var filter = CreateTransformFilter(root, orFilter);
            AddFilter(filter);
            return filter;
        }

        public static RaycastFilter AddBlockingFilter(Transform root)
        {
            var filter = CreateTransformBlockingFilter(root);
            AddFilter(filter);
            return filter;
        }

        public static RaycastFilter CreateTransformFilter(Transform root, RaycastFilter orFilter = null)
        {
            // cache all transforms
            var childReceivers = root.GetComponentsInChildren<IRaycastReceiver>(true).ToHashSet();

            bool Filter(IRaycastReceiver r)
            {
                if (childReceivers.Contains(r) || (orFilter != null && orFilter(r)))
                    return true;

                return ((Component)r).transform.IsChildOf(root);
            }
            
            return Filter;
        }

        public static RaycastFilter CreateTransformBlockingFilter(Transform root)
        {
            // cache all transforms
            var childReceivers = root.GetComponentsInChildren<IRaycastReceiver>(true).ToHashSet();
            return Filter;

            bool Filter(IRaycastReceiver r)
            {
                if (childReceivers.Contains(r))
                    return false;

                return !((Component)r).transform.IsChildOf(root);
            }
        }

        public static void RemoveFilter(RaycastFilter filter)
        {
            if (filter == null)
                Debug.LogWarning("trying to remove a null filter");
            else if (!TryRemoveFilter(filter))
                Debug.LogWarning($"trying to remove a filter that is no longer registered: {filter.Method.Name}");
        }
        
        public static bool TryRemoveFilter(RaycastFilter filter)
        {
            return filters.Remove(filter);
        }

        public static void ClearFilters(Predicate<RaycastFilter> predicate = null)
        {
            for (var i = filters.Count - 1; i >= 0; i--)
            {
                if (predicate == null || predicate(filters[i]))
                    filters.RemoveAt(i);
            }
        }
        public static List<RaycastFilter> GetFilters() => filters;

        /// <summary>
        /// Handle a press event from this controller.
        /// </summary>
        protected void HandlePressed()
        {
            bool wasCurrent = current;
            
            lastControllerPressed = this;
            foreach (var other in mutuallyExclusiveControllers)
            {
                other.HandleOtherPressed(this);
            }

            // When changing controllers, the first click shouldn't actually do anything besides changing controllers
            if (!wasCurrent)
                return;

            pressStartFrame = Time.frameCount;

            if (onAnyPress != null)
            {
                var args = new PressAnywhereEvent { controller = this, propagate = true };
                onAnyPress.Invoke(args);
                if (!args.propagate)
                    return;
            }
        }

        private void HandleOtherPressed(RaycastController controller)
        {
            lastControllerPressed = controller;
            // foreach enumerator not cached for interfaces on IL2CPP
            for (var i = 0; i < lastReceivers.Count; i++)
            {
                var receiver = lastReceivers[i];
                receiver.ClearHit(this);
            }

            lastReceivers.Clear();
        }

        void Update()
        {
            didHit = false;

            if (!current)
                return;

            var pos = _transform.position;
            var f = _transform.forward;
            Vector3 origin = pos - f * backRayLength;
            Vector3 direction = f;

            ray = new Ray(origin, direction);
            displayRay = new Ray(pos, direction);
            Debug.DrawRay(ray.origin, ray.direction * rayLength, Color.blue);
            
            // cast to find all hits in range and layer
            int numHits = Physics.RaycastNonAlloc(ray, hits, rayLength, layerMask, QueryTriggerInteraction.Collide);
            
            // sort hits by distance
            Array.Sort(hits, 0, numHits, raycastDistanceComparer);

            var sameAsLast = false;
            hit = new RaycastHit();
            List<IRaycastReceiver> closestReceivers = null;
            
            using var pooledObjectA = ListPool<IRaycastReceiver>.Get(out var potentialReceiversA);
            using var pooledObjectB = ListPool<IRaycastReceiver>.Get(out var potentialReceiversB);
            

            // find closest hit, prefer receivers that were hit last frame
            for (int i = 0; i < numHits; ++i)
            {
                // short circuit if we already found a hit and this hit is further than the last one
                if (didHit && !Mathf.Approximately(hit.distance, hits[i].distance))
                    break;

                using (ListPool<IRaycastReceiver>.Get(out var receiversBeforeFilter))
                {
                    hits[i].collider.gameObject.GetComponents(receiversBeforeFilter);
                    if (receiversBeforeFilter.Count != 0)
                    {
                        // filter hit
                        potentialReceiversA.Clear();
                        // foreach enumerator not cached for interfaces on IL2CPP
                        for (var j = 0; j < receiversBeforeFilter.Count; j++)
                        {
                            var receiver = receiversBeforeFilter[j];
                            if (receiver is MonoBehaviour { isActiveAndEnabled: false })
                                continue;

                            using var _ = ListPool<IRaycastReceiver>.Get(out var receivers);
                            receiver.Resolve(receivers);
                            foreach (var r in receivers)
                            {
                                if (r is MonoBehaviour { isActiveAndEnabled: false })
                                    continue;
                                if (filters.Count > 0 && !filters[^1](r))
                                    continue;
                                potentialReceiversA.Add(r);
                            }
                        }

                        if (potentialReceiversA.Count == 0)
                            continue;

                        // save hit
                        hit = hits[i];
                        didHit = true;
                        closestReceivers = potentialReceiversA;

                        // check if this is the same hit as last frame
                        if (lastReceivers.Count == closestReceivers.Count)
                        {
                            sameAsLast = true;
                            for (var j = 0; j < lastReceivers.Count; j++)
                            {
                                if (lastReceivers[j] != closestReceivers[j])
                                {
                                    sameAsLast = false;
                                    break;
                                }
                            }
                        }

                        // short circuit if this is the same hit as last frame
                        if (sameAsLast)
                            break;

                        // swap pointers
                        (potentialReceiversA, potentialReceiversB) = (potentialReceiversB, potentialReceiversA);
                    }
                }
            }

            // foreach enumerator not cached for interfaces on IL2CPP
            for (var i = 0; i < lastReceivers.Count; i++)
            {
                var receiver = lastReceivers[i];
                if (
                    // no new receivers
                    closestReceivers == null
                    // this receiver is no longer relevant
                    || !closestReceivers.Contains(receiver))
                {
                    try
                    {
                        receiver.ClearHit(this);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e, receiver as MonoBehaviour);
                    }
                }
            }

            lastReceivers.Clear();

            lastEventResult = IRaycastController.RaycastEvent.Result.Default;
            if (closestReceivers != null)
            {
                didHit = true;
                // foreach enumerator not cached for interfaces on IL2CPP
                for (var i = 0; i < closestReceivers.Count; i++)
                {
                    var receiver = closestReceivers[i];
                    var hitEvent = new IRaycastController.RaycastEvent
                    {
                        hit = hit,
                        result = IRaycastController.RaycastEvent.Result.Default
                    };
                    try
                    {
                        receiver.ReceiveHit(this, ref hitEvent);
                        if (hitEvent.result != IRaycastController.RaycastEvent.Result.Default)
                            lastEventResult = hitEvent.result;

                        lastReceivers.Add(receiver);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e, receiver as MonoBehaviour);
                    }
                }
            }
            
            // clear hits to avoid leaking references
            for (int i = 0; i < hits.Length; ++i)
                hits[i] = default;
        }

        private void OnDrawGizmos()
        {
            if (!didHit)
                return;

            Gizmos.color = debugHitColor;
            Gizmos.DrawWireSphere(hit.point, .025f);

            if (hit.collider == null)
                return;

            Gizmos.matrix = hit.collider.transform.localToWorldMatrix;
            Gizmos.color = debugColliderColor;
            DrawCollider(hit.collider);
        }

        private void DrawCollider(Collider c)
        {
            switch (c)
            {
                case BoxCollider box:
                    Gizmos.DrawWireCube(box.center, box.size);
                    break;
                case SphereCollider sphere:
                    Gizmos.DrawWireSphere(sphere.center, sphere.radius);
                    break;
            }

            // TODO more, maybe with collider.bounds, just need to understand in what space
        }
        
        #if UNITY_EDITOR
        [UnityEditor.MenuItem("Tools/Dexterity/Select active receiver %&r")]
        private static void SelectActiveReceiver()
        {
            var controller = FindObjectsOfType<RaycastController>().FirstOrDefault(c => c.current);
            if (controller == null || !controller.didHit || controller.hit.collider == null)
            {
                Debug.LogWarning($"No active receiver found", controller);
                return;
            }
            
            UnityEditor.Selection.activeObject = controller.hit.collider.gameObject;
            UnityEditor.SceneView.FrameLastActiveSceneView();
        }
        #endif

        public virtual bool isPressed => false;
        bool IRaycastController.isPressed => isPressed;
        bool IRaycastController.CompareTag(string other) => gameObject.CompareTag(other);
        bool IRaycastController.wasPressedThisFrame => pressStartFrame == Time.frameCount;
        Vector3 IRaycastController.position => transform.position;
        Vector3 IRaycastController.forward => transform.forward;
        Vector3 IRaycastController.up => transform.up;
        
        public virtual Vector2 scroll => Vector2.zero;
    }
}