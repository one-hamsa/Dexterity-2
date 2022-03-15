using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class AlphaModifier : ComponentModifier<CanvasGroup>, ISupportPropertyFreeze
    {
        [Serializable]
        public class Property : PropertyBase
        {
            // custom params
            public float alpha = 1f;
        }

        public override void Update()
        {
            base.Update();

            if (!transitionChanged)
                return;

            var alpha = 0f;
            foreach (var kv in transitionState)
            {
                var property = GetProperty(kv.Key) as Property;
                var value = kv.Value;

                alpha += property.alpha * value;
            }

            component.alpha = alpha;
        }

        public void FreezeProperty(PropertyBase property)
        {
            var prop = property as Property;
            prop.alpha = component.alpha;
        } 
    }
}
