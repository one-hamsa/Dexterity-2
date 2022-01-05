using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class OrientationModifier : Modifier, ISupportPropertyFreeze
    {
        [Serializable]
        public class Property : PropertyBase
        {
            // custom params
            public Vector3 position;
            [EulerAngles]
            public Quaternion rotation;
        }

        public override void Update()
        {
            base.Update();

            if (!transitionChanged)
                return;

            var rotationOffset = Quaternion.identity;
            var positionOffset = Vector3.zero;
            foreach (var kv in transitionState)
            {
                var property = GetProperty(kv.Key) as Property;
                var value = kv.Value;

                positionOffset += Vector3.Lerp(Vector3.zero, property.position, value);
                rotationOffset = Quaternion.Lerp(rotationOffset, property.rotation, value);
            }

            transform.localPosition = positionOffset;
            transform.localRotation = rotationOffset;
        }

        public void FreezeProperty(PropertyBase property)
        {
            var prop = property as Property;
            prop.position = transform.localPosition;
            prop.rotation = transform.localRotation;
        } 
    }
}
