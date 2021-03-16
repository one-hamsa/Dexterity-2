using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class ScaleModifier : Modifier
    {
        public float BaseScale = 1f;

        [Serializable]
        public class Property : PropertyBase
        {
            // custom params
            public float Scale = 1f;
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

                scale += Vector3.one * BaseScale * property.Scale * value;
            }

            transform.localScale = scale;
        }
    }
}
