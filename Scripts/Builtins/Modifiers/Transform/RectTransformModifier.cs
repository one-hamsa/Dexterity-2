using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class RectTransformModifier : ComponentModifier<RectTransform>, ISupportValueFreeze, ISupportPropertyFreeze
    {
        public Vector2 baseSize;

        [Serializable]
        public class Property : PropertyBase
        {
            // custom params
            public Vector2 sizeDelta;
        }

        public override void Update()
        {
            base.Update();

            if (!transitionChanged)
                return;

            Vector2 sizeDelta = baseSize;
            foreach (var kv in transitionState)
            {
                var property = GetProperty(kv.Key) as Property;
                var value = kv.Value;

                sizeDelta += Vector2.Lerp(Vector2.zero, property.sizeDelta, value);
            }

            component.sizeDelta = sizeDelta;
        }

        public void FreezeProperty(PropertyBase property)
        {
            var prop = property as Property;
            prop.sizeDelta = component.sizeDelta - baseSize;
        }

        public void FreezeValue()
        {
            baseSize = component.sizeDelta;
        }
    }
}
