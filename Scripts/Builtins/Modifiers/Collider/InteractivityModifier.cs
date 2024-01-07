using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using OneHamsa.Dexterity.Utilities;
using UnityEngine.Pool;

namespace OneHamsa.Dexterity.Builtins
{
    public class InteractivityModifier : Modifier
    {
        public bool recursive = true;
        private List<Collider> cachedColliders = new();
        public override bool animatableInEditor => false;

        private static Dictionary<Collider, HashSet<InteractivityModifier>> colliderDisabledBy = new();
        private HierarchyListener hierarchyListener;
        private bool dirty;

        [Serializable]
        public class Property : PropertyBase
        {
            // custom params
            public bool interactive;
        }

        protected override void OnEnable()
        {
            dirty = true;

            if (recursive)
            {
                hierarchyListener = gameObject.GetOrAddComponent<HierarchyListener>();
                hierarchyListener.hideFlags = HideFlags.HideInInspector | HideFlags.HideAndDontSave;

                hierarchyListener.onChildAdded += OnChildAdded;
                hierarchyListener.onChildRemoved += OnChildRemoved;
            }

            base.OnEnable();

        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (hierarchyListener != null)
            {
                hierarchyListener.onChildAdded -= OnChildAdded;
                hierarchyListener.onChildRemoved -= OnChildRemoved;
            }
            
            PruneStaleColliders();
            
            foreach (var c in cachedColliders)
            {
                if (!colliderDisabledBy.TryGetValue(c, out var disablers))
                    colliderDisabledBy[c] = disablers = new HashSet<InteractivityModifier>();
                
                disablers.Remove(this);
                
                c.enabled = disablers.Count == 0;
            }
        }

        private void OnChildAdded(Transform child)
        {
            // can be optimized - look only at child
            dirty = true;
        }

        private void OnChildRemoved(Transform child)
        {
            // can be optimized - look only at child
            dirty = true;
        }

        private void LateUpdate()
        {
            if (dirty)
            {
                dirty = false;
                
                // collect colliders
                RefreshTrackedColliders();
                // update colliders
                HandleStateChange(GetNodeActiveStateWithoutDelay(), GetNodeActiveStateWithoutDelay());
            }
        }

        private void RefreshTrackedColliders()
        {
            cachedColliders.Clear();
            if (recursive)
                GetComponentsInChildren(true, cachedColliders);
            else
                GetComponents(cachedColliders);
        }

        public override void HandleStateChange(int oldState, int newState) {
            base.HandleStateChange(oldState, newState);
            
            PruneStaleColliders();
            
            var property = (Property)GetProperty(newState);
            var shouldDisable = !property.interactive;

            foreach (var c in cachedColliders)
            {
                if (!colliderDisabledBy.TryGetValue(c, out var disablers))
                    colliderDisabledBy[c] = disablers = new HashSet<InteractivityModifier>();

                if (shouldDisable)
                    disablers.Add(this);
                else
                    disablers.Remove(this);

                c.enabled = disablers.Count == 0;
            }
        }

        /// <summary>
        /// Removes colliders that are destroyed or no longer children of this transform
        /// </summary>
        private void PruneStaleColliders()
        {
            // in-place cleanup
            for (var i = cachedColliders.Count - 1; i >= 0; i--) 
            {
                if (cachedColliders[i] == null || !cachedColliders[i].transform.IsChildOf(transform))
                {
                    if (colliderDisabledBy.TryGetValue(cachedColliders[i], out var disablers))
                        disablers.Remove(this);
                    cachedColliders.RemoveAt(i);
                }
            }
            
            // use aux list to avoid allocations
            using var _ = ListPool<Collider>.Get(out var colliderDisabledByTmp);
            foreach (var c in colliderDisabledBy.Keys)
            {
                if (c == null)
                    colliderDisabledByTmp.Add(c);
            }
            
            foreach (var c in colliderDisabledByTmp)
                colliderDisabledBy.Remove(c);
        }
        
        public override void Refresh()
        {
	        base.Refresh();
	        // always mark as didn't change
	        transitionChanged = false;
        }
    }
}
