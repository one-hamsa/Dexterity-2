using System;
using UnityEngine;

namespace OneHamsa.Dexterity.Builtins
{
    public class WhitelistRaycastModifier : ComponentModifier<WhitelistRaycastFilter>
    {
        protected override bool createIfNotFound => true;

        [Serializable]
        public class Property : PropertyBase
        {
            // custom params
            public bool filter;
        }

        public override void HandleStateChange(int oldState, int newState) {
            base.HandleStateChange(oldState, newState);
            
            var property = (Property)GetProperty(newState);
            component.enabled = property.filter;
        }
    }
}
