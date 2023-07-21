using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace OneHamsa.Dexterity.Builtins
{
    public class InteractivityModifier : Modifier
    {
        public bool recursive = true;
        private List<Collider> cachedColliders;

        private bool _overrideDisable;
        public bool overrideDisable { set { _overrideDisable = value; HandleStateChange(_node.GetActiveState(), _node.GetActiveState()); }}
        public override bool animatableInEditor => false;

        private static Dictionary<Collider, HashSet<InteractivityModifier>> colliderDisabledBy = new();
        private static List<Collider> colliderDisabledByTmp = new();

        [Serializable]
        public class Property : PropertyBase
        {
            // custom params
            public bool interactive;
        }

        protected override void Awake()
        {
            base.Awake();

            RefreshTrackedColliders();
        }

        public void RefreshTrackedColliders()
        {
            cachedColliders = recursive
                ? GetComponentsInChildren<Collider>(true).Where(c => c.enabled).ToList()
                : GetComponents<Collider>().ToList();
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

                if (shouldDisable || _overrideDisable)
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
    }
}
