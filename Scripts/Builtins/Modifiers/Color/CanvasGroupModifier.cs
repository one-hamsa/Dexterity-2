using UnityEngine;
using System;

namespace OneHamsa.Dexterity.Builtins
{
    public class CanvasGroupModifier : ComponentModifier<CanvasGroup>, ISupportPropertyFreeze
    { 
        [Serializable]
        public class Property : PropertyBase
        {
            // custom params
            public float alpha;
        }

        public override void Refresh()
        {
            base.Refresh();

            if (!transitionChanged)
                return;

            float alpha = 0f;
            foreach (var kv in transitionState.keyValuePairs)
            {
                var property = (Property)GetProperty(kv.Key);
                var value = kv.Value;

                alpha += Mathf.Lerp(0, property.alpha, value);
            }

            component.alpha = alpha;
        }

        public void FreezeProperty(PropertyBase property)
        {
            if (component == null)
                return;

            var prop = (Property)property;
            prop.alpha = component.alpha;
        }
    }
}
