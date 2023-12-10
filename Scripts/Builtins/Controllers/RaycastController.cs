using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;
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
        }
        
        const float rayLength = 100f;
        const int maxHits = 20;

        // Raycast Receiver filter
        private static readonly List<RaycastFilter> filters = new();
        public static event Action<PressAnywhereEvent> onAnyPress;

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
        private List<IRaycastReceiver> potentialReceiversA = new(4);
        private List<IRaycastReceiver> potentialReceiversB = new(4);
        private readonly List<IRaycastReceiver> receiversBeforeFilter = new(1);
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
            // cache all transforms
            var childReceivers = root.GetComponentsInChildren<IRaycastReceiver>(true).ToHashSet();

            bool Filter(IRaycastReceiver r)
            {
                if (childReceivers.Contains(r) || (orFilter != null && orFilter(r)))
                    return true;

                return ((Component)r).transform.IsChildOf(root);
            }

            AddFilter(Filter);
            return Filter;
        }

        public static RaycastFilter AddBlockingFilter(Transform root)
        {
            // cache all transforms
            var childReceivers = root.GetComponentsInChildren<IRaycastReceiver>(true).ToHashSet();

            bool Filter(IRaycastReceiver r)
            {
                if (childReceivers.Contains(r))
                    return false;

                return !((Component)r).transform.IsChildOf(root);
            }

            AddFilter(Filter);
            return Filter;
        }

        public static void RemoveFilter(RaycastFilter filter)
        {
            if (!TryRemoveFilter(filter))
                Debug.LogWarning($"trying to remove a filter that is no longer registered: {filter.Method.Name}");
        }
        
        public static bool TryRemoveFilter(RaycastFilter filter)
        {
            return filters.Remove(filter);
        }

        public static void ClearFilters()
        {
            if (filters.Count > 0)
            {
                // log this, because if we got to clearing filters it means someone didn't clean up after themself...!
                Debug.LogWarning($"clearing {filters.Count} filters");
                filters.Clear();
            }
        }

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

            int numHits = Physics.RaycastNonAlloc(ray, hits, rayLength, layerMask, QueryTriggerInteraction.Collide);
            
            // sort hits by distance
            Array.Sort(hits, 0, numHits, raycastDistanceComparer);

            var sameAsLast = false;
            hit = new RaycastHit();
            List<IRaycastReceiver> closestReceivers = null;

            // find closest hit, prefer receivers that were hit last frame
            for (int i = 0; i < numHits; ++i)
            {
                // short circuit if we already found a hit and this hit is further than the last one
                if (didHit && !Mathf.Approximately(hit.distance, hits[i].distance))
                    break;
                
                hits[i].collider.gameObject.GetComponents(receiversBeforeFilter);
                if (receiversBeforeFilter.Count != 0)
                {
                    // filter hit
                    potentialReceiversA.Clear();
                    // foreach enumerator not cached for interfaces on IL2CPP
                    for (var j = 0; j < receiversBeforeFilter.Count; j++)
                    {
                        var receiver = receiversBeforeFilter[j];
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

        protected virtual bool isPressed => false;
        bool IRaycastController.isPressed => isPressed;
        bool IRaycastController.CompareTag(string other) => gameObject.CompareTag(other);
        bool IRaycastController.wasPressedThisFrame => pressStartFrame == Time.frameCount;
        Vector3 IRaycastController.position => transform.position;
        Vector3 IRaycastController.forward => transform.forward;
        Vector3 IRaycastController.up => transform.up;
    }
}