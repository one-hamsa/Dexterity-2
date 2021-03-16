using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class Scale3DModifier : Modifier
    {
        public Vector3 BaseScale = Vector3.one;

        [Serializable]
        public class Property : PropertyBase
        {
            // custom params
            public Vector3 Scale = Vector3.one;
        }

        protected override void Update()
        {
            base.Update();

            if (!transitionChanged)
                return;

            var scale = Vector3.zero;
            foreach (var kv in transitionState)
            {
                var property = GetProperty(kv.Key) as Property;
                var value = kv.Value;

                scale += property.Scale * value;
            }
            scale.x *= BaseScale.x;
            scale.y *= BaseScale.y;
            scale.x *= BaseScale.z;

            transform.localScale = scale;
        }
    }
}
