using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class Scale3DModifier : Modifier, ISupportValueFreeze, ISupportPropertyFreeze
    {
        public Vector3 baseScale = Vector3.one;

        [Serializable]
        public class Property : PropertyBase
        {
            // custom params
            public Vector3 scale = Vector3.one;
        }

        public override void Update()
        {
            base.Update();

            if (!transitionChanged)
                return;

            var scale = Vector3.zero;
            foreach (var kv in transitionState)
            {
                var property = GetProperty(kv.Key) as Property;
                var value = kv.Value;

                scale += property.scale * value;
            }
            scale.x *= baseScale.x;
            scale.y *= baseScale.y;
            scale.x *= baseScale.z;

            transform.localScale = scale;
        }

        public void FreezeValue()
        {
            baseScale = transform.localScale;
        }
        public void FreezeProperty(PropertyBase property)
        {
            var prop = property as Property;
            prop.scale = transform.localScale;
        }
    }
}