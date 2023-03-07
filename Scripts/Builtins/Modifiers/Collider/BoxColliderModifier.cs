using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class BoxColliderModifier : ComponentModifier<BoxCollider>, ISupportValueFreeze, ISupportPropertyFreeze
    {
        public Vector3 baseCenter;
        public Vector3 baseSize;

        [Serializable]
        public class Property : PropertyBase
        {
            // custom params
            public Vector3 centerDelta;
            public Vector3 sizeDelta;
        }

        public override void Refresh()
        {
            base.Refresh();

            if (!transitionChanged)
                return;

            Vector3 center = baseCenter;
            Vector3 size = baseSize;
            foreach (var kv in transitionState)
            {
                var property = GetProperty(kv.Key) as Property;
                var value = kv.Value;

                center += Vector3.Lerp(Vector3.zero, property.centerDelta, value);
                size += Vector3.Lerp(Vector3.zero, property.sizeDelta, value);
            }

            component.size = size;
            component.center = center;
        }

        public void FreezeProperty(PropertyBase property)
        {
            if (component == null)
                return;

            var prop = property as Property;
            prop.centerDelta = component.center - baseCenter;
            prop.sizeDelta = component.size - baseSize;
        }

        public void FreezeValue()
        {
            if (component == null)
                return;
                
            baseCenter = component.center;
            baseSize = component.size;
        }
    }
}
