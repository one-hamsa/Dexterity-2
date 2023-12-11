using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

namespace OneHamsa.Dexterity.Builtins
{
    public class TransformModifier : Modifier, ISupportValueFreeze, ISupportPropertyFreeze
    {
        public Vector3 basePosition;
        [EulerAngles]
        public Quaternion baseRotation;

        [Serializable]
        public class Property : PropertyBase
        {
            // custom params
            public Vector3 position;
            [EulerAngles]
            public Quaternion rotation = Quaternion.identity;
        }

        public override void Refresh()
        {
            base.Refresh();

            if (!transitionChanged)
                return;

            var rotationOffset = baseRotation;
            var positionOffset = basePosition;
            foreach (var kv in transitionState.keyValuePairs)
            {
                var property = GetProperty(kv.Key) as Property;
                var value = kv.Value;

                positionOffset += Vector3.Lerp(Vector3.zero, property.position, value);
                rotationOffset = Quaternion.Slerp(rotationOffset, baseRotation * property.rotation, value);
            }

            _transform.SetLocalPositionAndRotation(positionOffset, rotationOffset);
        }

        public void FreezeProperty(PropertyBase property)
        {
            var prop = property as Property;
            transform.GetLocalPositionAndRotation(out var localPos, out var localRot);
            prop.position = localPos - basePosition;
            prop.rotation = localRot * Quaternion.Inverse(baseRotation);
        } 

        public void FreezeValue()
        {
            transform.GetLocalPositionAndRotation(out basePosition, out baseRotation);
        }
    }
}
