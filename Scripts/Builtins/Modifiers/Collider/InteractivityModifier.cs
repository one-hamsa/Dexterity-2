using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using OneHamsa.Dexterity.Utilities;

namespace OneHamsa.Dexterity.Builtins
{
    public class InteractivityModifier : Modifier
    {
        public bool recursive = true;
        private List<Collider> cachedColliders = new();
        public override bool animatableInEditor => false;

        private static Dictionary<Collider, HashSet<InteractivityModifier>> colliderDisabledBy = new();
        private static List<Collider> colliderDisabledByTmp = new();
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
            
            PruneDeadColliders();
            
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

        private void PruneDeadColliders()
        {
            // in-place cleanup
            for (var i = cachedColliders.Count - 1; i >= 0; i--) {
                if (cachedColliders[i] == null) 
                    cachedColliders.RemoveAt(i);
            }
            
            // use aux list to avoid allocations
            colliderDisabledByTmp.Clear();
            foreach (var c in colliderDisabledBy.Keys)
            {
                if (c == null)
                    colliderDisabledByTmp.Add(c);
            }
            
            foreach (var c in colliderDisabledByTmp)
                colliderDisabledBy.Remove(c);
        }

        public void OnDestroy()
        {
            PruneDeadColliders();
            
            foreach (var c in cachedColliders)
            {
                if (!colliderDisabledBy.TryGetValue(c, out var disablers))
                    colliderDisabledBy[c] = disablers = new HashSet<InteractivityModifier>();
                
                disablers.Remove(this);
                
                c.enabled = disablers.Count == 0;
            }
        }
        
        public override void Refresh()
        {
	        base.Refresh();
	        // always mark as didn't change
	        transitionChanged = false;
        }
    }
}
