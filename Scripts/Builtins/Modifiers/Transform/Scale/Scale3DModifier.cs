using UnityEngine;
using System;
using UnityEngine.UI;

namespace OneHamsa.Dexterity.Builtins
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

        public override void Refresh()
        {
            base.Refresh();

            if (!transitionChanged)
                return;

            var scale = Vector3.zero;
            foreach (var kv in transitionState.keyValuePairs)
            {
                var property = GetProperty(kv.Key) as Property;
                var value = kv.Value;

                scale += property.scale * value;
            }
            scale.x *= baseScale.x;
            scale.y *= baseScale.y;
            scale.z *= baseScale.z;

            _transform.localScale = scale;
            
            if (_transform is RectTransform rectTransform)
                LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
        }

        public void FreezeValue()
        {
            baseScale = transform.localScale;
        }
        public void FreezeProperty(PropertyBase property)
        {
            var prop = property as Property;
            prop.scale = new Vector3(transform.localScale.x / baseScale.x, 
                                     transform.localScale.y / baseScale.y, 
                                     transform.localScale.z / baseScale.z);
        }
    }
}
