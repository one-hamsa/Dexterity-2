using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class InteractivityModifier : Modifier
    {
        public bool recursive = true;
        private List<Collider> cachedColliders;
        
        private static Dictionary<Collider, HashSet<InteractivityModifier>> colliderDisabledBy = new();

        [Serializable]
        public class Property : PropertyBase
        {
            // custom params
            public bool interactive;
        }

        public override void Awake()
        {
            base.Awake();

            cachedColliders = recursive 
                ? GetComponentsInChildren<Collider>(true).Where(c => c.enabled).ToList() 
                : GetComponents<Collider>().ToList();
        }

        public override void HandleStateChange(int oldState, int newState)
        {
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

        public override void OnDestroy()
        {
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
