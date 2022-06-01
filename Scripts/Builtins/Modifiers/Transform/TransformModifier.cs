using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class TransformModifier : Modifier, ISupportValueFreeze, ISupportPropertyFreeze
    {
        public Vector3 basePosition;
        public Vector3 baseRotation;

        [Serializable]
        public class Property : PropertyBase
        {
            // custom params
            public Vector3 position;
            [EulerAngles]
            public Quaternion rotation = Quaternion.identity;
        }

        public override void Update()
        {
            base.Update();

            if (!transitionChanged)
                return;

            Quaternion baseRotationQ = Quaternion.Euler(baseRotation);
            var rotationOffset = baseRotationQ;
            var positionOffset = basePosition;
            foreach (var kv in transitionState)
            {
                var property = GetProperty(kv.Key) as Property;
                var value = kv.Value;

                positionOffset += Vector3.Lerp(Vector3.zero, property.position, value);
                rotationOffset = Quaternion.Slerp(rotationOffset, baseRotationQ * property.rotation, value);
            }

            transform.localPosition = positionOffset;
            transform.localRotation = rotationOffset;
        }

        public void FreezeProperty(PropertyBase property)
        {
            var prop = property as Property;
            prop.position = transform.localPosition - basePosition;
            prop.rotation = transform.localRotation * Quaternion.Inverse(Quaternion.Euler(baseRotation));
        } 

        public void FreezeValue()
        {
            // if (transform == null)
            //     return;

            basePosition = transform.localPosition;
            baseRotation = transform.localEulerAngles;
        }
    }
}
