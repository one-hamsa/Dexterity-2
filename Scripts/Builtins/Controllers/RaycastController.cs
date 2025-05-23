using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using Debug = UnityEngine.Debug;
using OneHamsa.Dexterity.Utilities;

namespace OneHamsa.Dexterity.Builtins
{
    public class RaycastController : MonoBehaviour, IRaycastController
    {
        
        public class PressAnywhereEvent
        {
            public RaycastController controller;
            public bool propagate;
            public readonly int id = nextId++;
            private static int nextId = 0;

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
        
        const float rayLength = 500f;
        const int maxHits = 20;

        // RaycastReceiver filter
        private static readonly List<RaycastFilter> filters = new();

        // custom raycast resolvers
        private static readonly List<IRaycastResolver> resolvers = new();
        public static event PressAnywhereHandler onAnyPress;

        public LayerMask layerMask = int.MaxValue;
        [Tooltip("How far back should the ray be casted from this transform?")]
        public float backRayLength = .25f;

        public RaycastController[] mutuallyExclusiveControllers;
        public bool defaultController;

        public static void AddResolver(IRaycastResolver resolver)
        {
            resolvers.Add(resolver);
        }
        public static void RemoveResolver(IRaycastResolver resolver)
        {
            resolvers.Remove(resolver);
        }

        [Header("Debug")] public Color debugColliderColor = new Color(1f, .5f, 0f);
        public Color debugHitColor = new Color(1f, .25f, 0f);

        public bool current => enabled && ((ReferenceEquals(lastControllerPressed, null) && defaultController) || ReferenceEquals(lastControllerPressed, this));

        public bool showVisibleRay { get;  set; } = true;

        public Ray ray { get; private set; }
        public Ray displayRay { get; private set; }
        public bool didHit { get; private set; }
        public DexterityRaycastHit hit { get; private set; }
        private int pressStartFrame = -1;
        private RaycastController lastControllerPressed;

        private readonly DexterityRaycastHit[] hits = new DexterityRaycastHit[maxHits];
        private readonly RaycastHit[] colldierHits = new RaycastHit[maxHits];
        private readonly List<IRaycastReceiver> lastReceivers = new(4);
        private IRaycastController.RaycastResult lastRaycastResult;

        [Preserve]
        public IRaycastController.RaycastResult.Result GetLastEventResult() => lastRaycastResult.result;

        [Preserve]
        public IRaycastReceiver GetLastEventResultReceiver() => lastRaycastResult.hitReceiver;

        public delegate bool RaycastFilter(IRaycastReceiver receiver);

        protected Transform _transform;

        private static readonly Comparer<DexterityRaycastHit> raycastDistanceComparer
            = Comparer<DexterityRaycastHit>.Create((a, b) =>
            {
                if (a.priority != IRaycastPriorityGroup.IGNORE_PRIORITY && 
                    b.priority != IRaycastPriorityGroup.IGNORE_PRIORITY && 
                    a.priority != b.priority)
                    return a.priority.CompareTo(b.priority);
                return a.distance.CompareTo(b.distance);
            });
        
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
            // Cache all instance IDs of transforms
            var childReceiverIds = root.GetComponentsInChildren<IRaycastReceiver>(true)
                .Select(r => ((Component)r).GetInstanceID())
                .ToHashSet();

            bool Filter(IRaycastReceiver r)
            {
                var id = ((Component)r).GetInstanceID();
                if (childReceiverIds.Contains(id) || (orFilter != null && orFilter(r)))
                    return true;

                return ((Component)r).transform.IsChildOf(root);
            }
            
            return Filter;
        }

        public static RaycastFilter CreateTransformBlockingFilter(Transform root)
        {
            // cache all instance IDs of transforms
            var childReceiverIds = root.GetComponentsInChildren<IRaycastReceiver>(true)
                .Select(r => ((Component)r).GetInstanceID())
                .ToHashSet();

            return Filter;

            bool Filter(IRaycastReceiver r)
            {
                var id = ((Component)r).GetInstanceID();
                if (childReceiverIds.Contains(id))
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

        public readonly struct FilterContext : IDisposable
        {
            private readonly RaycastFilter filter;

            public FilterContext(RaycastFilter filter)
            {
                this.filter = filter;
                AddFilter(filter);
            }

            public void Dispose()
            {
                RemoveFilter(filter);
            }
        }
        public static FilterContext DisableAll => new(_ => false);

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
                var args = new PressAnywhereEvent { controller = this, propagate = true, };
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
            int numHits = Physics.RaycastNonAlloc(ray, colldierHits, rayLength, layerMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < numHits; i++)
            {
                var dexHit = hits[i];
                var hit = colldierHits[i];
                dexHit.distance = hit.distance;
                dexHit.point = hit.point;
                dexHit.normal = hit.normal;
                dexHit.transform = hit.collider.transform;
                dexHit.collider = hit.collider;
                dexHit.priority = IRaycastPriorityGroup.GetPriority(dexHit);
                if (dexHit.priority >= IRaycastPriorityGroup.ABORT_PRIORITY)
                {
                    // Abort this hit
                    colldierHits[i] = colldierHits[numHits-1];
                    numHits--;
                    i--;
                }
                else
                    hits[i] = dexHit;
            }
            
            for (int i = 0; i < resolvers.Count; i++)
            {
                if (numHits < hits.Length)
                {
                    //we have room for resolvers
                    var resolver = resolvers[i];
                    if ((layerMask & resolver.GetLayerMask()) > 0 && resolver.GetHit(ray, out var resolvedHit))
                    {
                        var hitIndex = Mathf.Min(numHits, hits.Length);
                        hits[hitIndex] = resolvedHit;
                        numHits++;
                    }
                }
            }

            // sort hits by distance
            Array.Sort(hits, 0, numHits, raycastDistanceComparer);

            var sameAsLast = false;
            hit = new DexterityRaycastHit();
            List<IRaycastReceiver> closestReceivers = null;
            
            using var pooledObjectA = ListPool<IRaycastReceiver>.Get(out var potentialReceiversA);
            using var pooledObjectB = ListPool<IRaycastReceiver>.Get(out var potentialReceiversB);
            

            // find closest hit, prefer receivers that were hit last frame
            for (int i = 0; i < numHits; ++i)
            {
                // short circuit if we already found a hit and this hit is further than the last one (or lower priority)
                if (didHit && (hit.priority < hits[i].priority || !Mathf.Approximately(hit.distance, hits[i].distance)))
                    break;

                using (ListPool<IRaycastReceiver>.Get(out var receiversBeforeFilter))
                using (ListPool<IRaycastReceiver>.Get(out var tempReceivers))
                {
                    receiversBeforeFilter.Clear();
                    
                    // find the top receiver in the hierarchy
                    var topReceiver = hits[i].transform.gameObject.GetComponentInParent<IRaycastReceiver>();
                    // then get all receivers on the same object
                    // now add rest of hierarchy
                    var currentReceiver = topReceiver;
                    while (currentReceiver != null)
                    {
                        ((MonoBehaviour)currentReceiver).gameObject.GetComponents(tempReceivers);
                        receiversBeforeFilter.AddRange(tempReceivers);

                        bool shouldBlockParent = ((MonoBehaviour)currentReceiver).TryGetComponent(out IBlockRaycastParentPropagation _);
                        if (shouldBlockParent)
                            break;

                        currentReceiver = ((MonoBehaviour)currentReceiver).transform.parent?.GetComponentInParent<IRaycastReceiver>();
                    }

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
                    || !closestReceivers.FastContains(receiver))
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

            lastRaycastResult = new IRaycastController.RaycastResult();
            if (closestReceivers != null)
            {
                didHit = true;
                bool bHaveResults = false;
                // foreach enumerator not cached for interfaces on IL2CPP
                for (var i = 0; i < closestReceivers.Count; i++)
                {
                    var receiver = closestReceivers[i];
                    var hitEvent = new IRaycastController.RaycastResult();
                    try
                    {
                        receiver.ReceiveHit(this, ref hitEvent);
                        if (hitEvent.hitReceiver != null && !bHaveResults)
                        {
                            lastRaycastResult = hitEvent;
                            bHaveResults = true;
                        }

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

        public virtual bool isPressed => false;
        public bool wasPressedThisFrame => pressStartFrame == Time.frameCount;
        bool IRaycastController.CompareTag(string other) => gameObject.CompareTag(other);
        Vector3 IRaycastController.position => transform.position;
        Vector3 IRaycastController.forward => transform.forward;
        Vector3 IRaycastController.up => transform.up;
        
        public virtual Vector2 scroll => Vector2.zero;
    }
}
