using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
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
        public float repeatHitCooldown = 0f;

        public RaycastController[] mutuallyExclusiveControllers;
        public bool defaultController;

        [Header("Debug")] public Color debugColliderColor = new Color(1f, .5f, 0f);
        public Color debugHitColor = new Color(1f, .25f, 0f);

        public bool current =>
            enabled && ((lastControllerPressed == null && defaultController) || lastControllerPressed == this);

        public Ray ray { get; private set; }
        public bool didHit { get; private set; }
        public RaycastHit hit { get; private set; }
        private int pressStartFrame = -1;
        private RaycastController lastControllerPressed;

        private readonly RaycastHit[] hits = new RaycastHit[maxHits];
        private readonly List<IRaycastReceiver> lastReceivers = new(4);
        private List<IRaycastReceiver> potentialReceiversA = new(4);
        private List<IRaycastReceiver> potentialReceiversB = new(4);
        private readonly List<IRaycastReceiver> receiversBeforeFilter = new(1);

        private readonly Dictionary<IRaycastReceiver, long> recentlyHitReceivers = new();
        private IRaycastController.RaycastEvent.Result lastEventResult;

        [Preserve]
        public IRaycastController.RaycastEvent.Result GetLastEventResult() => lastEventResult;

        public delegate bool RaycastFilter(IRaycastReceiver receiver);

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

                var t = ((Component)r).transform;
                while (t != null)
                {
                    if (t == root)
                        return true;
                    t = t.parent;
                }

                return false;
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

                var t = ((Component)r).transform;
                while (t != null)
                {
                    if (t == root)
                        return false;
                    t = t.parent;
                }

                return true;
            }

            AddFilter(Filter);
            return Filter;
        }

        public static void RemoveFilter(RaycastFilter filter)
        {
            if (!filters.Remove(filter))
                Debug.LogWarning($"trying to remove a filter that is no longer registered: {filter}");
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
            lastControllerPressed = this;
            foreach (var other in mutuallyExclusiveControllers)
            {
                other.HandleOtherPressed(this);
            }

            if (onAnyPress != null)
            {
                var args = new PressAnywhereEvent { controller = this, propagate = true };
                onAnyPress.Invoke(args);
                if (!args.propagate)
                    return;
            }

            pressStartFrame = Time.frameCount;
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
            long now = Stopwatch.GetTimestamp();

            didHit = false;

            if (!current)
                return;

            var t = transform;
            Vector3 origin = t.position;
            Vector3 direction = t.forward;

            ray = new Ray(origin, direction);
            Debug.DrawRay(ray.origin, ray.direction * rayLength, Color.blue);

            int numHits = Physics.RaycastNonAlloc(ray, hits, rayLength, layerMask, QueryTriggerInteraction.Collide);

            float minDist = float.PositiveInfinity;
            hit = new RaycastHit();
            List<IRaycastReceiver> closestReceivers = null;

            for (int i = 0; i < numHits; ++i)
            {
                hits[i].collider.gameObject.GetComponents(receiversBeforeFilter);
                if (receiversBeforeFilter.Count != 0 && hits[i].distance < minDist)
                {
                    // filter hit
                    potentialReceiversA.Clear();
                    // foreach enumerator not cached for interfaces on IL2CPP
                    for (var j = 0; j < receiversBeforeFilter.Count; j++)
                    {
                        var receiver = receiversBeforeFilter[j];
                        var r = receiver.Resolve();
                        if (filters.Count > 0 && !filters[^1](r))
                            continue;
                        potentialReceiversA.Add(r);
                    }

                    if (potentialReceiversA.Count == 0)
                        continue;

                    // save hit
                    hit = hits[i];
                    minDist = hits[i].distance;
                    closestReceivers = potentialReceiversA;
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
                    recentlyHitReceivers[receiver] = now;
                    receiver.ClearHit(this);
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

                    // repeat-hit cooldown
                    if (recentlyHitReceivers.TryGetValue(receiver, out var lastHit))
                    {
                        var cooldown = (float)(now - lastHit) / Stopwatch.Frequency;
                        if (cooldown < repeatHitCooldown)
                            continue;
                    }

                    var hitEvent = new IRaycastController.RaycastEvent
                    {
                        hit = hit,
                        result = IRaycastController.RaycastEvent.Result.Default
                    };
                    receiver.ReceiveHit(this, ref hitEvent);
                    if (hitEvent.result != IRaycastController.RaycastEvent.Result.Default)
                        lastEventResult = hitEvent.result;

                    lastReceivers.Add(receiver);
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

        protected virtual bool isPressed => false;
        bool IRaycastController.isPressed => isPressed;
        bool IRaycastController.CompareTag(string other) => gameObject.CompareTag(other);
        bool IRaycastController.wasPressedThisFrame => pressStartFrame == Time.frameCount;
        Vector3 IRaycastController.position => transform.position;
        Vector3 IRaycastController.forward => transform.forward;
    }
}