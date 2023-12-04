using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

namespace OneHamsa.Dexterity.Builtins
{
    public class ZTransformModifier : Modifier, ISupportValueFreeze, ISupportPropertyFreeze
    {
        public float baseZ;

        private Transform t;

        [Serializable]
        public class Property : PropertyBase
        {
            // custom params
            public float z;
        }

        protected override void Awake()
        {
            base.Awake();
            t = transform;
        }

        public override void Refresh()
        {
            base.Refresh();

            if (!transitionChanged)
                return;

            var zOffset = baseZ;
            foreach (var kv in transitionState.keyValuePairs)
            {
                var property = GetProperty(kv.Key) as Property;
                var value = kv.Value;

                zOffset += Mathf.Lerp(0f, property.z, value);
            }

            var localPosition = t.localPosition;
            localPosition = new Vector3(localPosition.x, localPosition.y, zOffset);
            t.localPosition = localPosition;
        }

        public void FreezeProperty(PropertyBase property)
        {
            var prop = property as Property;
            prop.z = transform.localPosition.z - baseZ;
        } 

        public void FreezeValue()
        {
            baseZ = transform.localPosition.z;
        }
    }
}
