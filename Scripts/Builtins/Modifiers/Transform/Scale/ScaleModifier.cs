using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class ScaleModifier : Modifier, ISupportValueFreeze, ISupportPropertyFreeze
    {
        public float baseScale = 1f;

        [Serializable]
        public class Property : PropertyBase
        {
            // custom params
            public float scale = 1f;
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

                scale += Vector3.one * baseScale * property.scale * value;
            }

            transform.localScale = scale;
        }

        public void FreezeValue()
        {
            baseScale = transform.localScale.x;
        }
        public void FreezeProperty(PropertyBase property)
        {
            var prop = property as Property;
            prop.scale = transform.localScale.x / baseScale;
        } 
    }
}
