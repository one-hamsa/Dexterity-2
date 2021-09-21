using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Events;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class UnityObjectField : BaseField
    {
        public UnityEngine.Object targetObject;
        [ObjectValue(objectFieldName: nameof(targetObject), fieldType: typeof(bool))]
        public string targetProperty;

        public override int GetValue() => ObjectValueAttribute.Read<bool>(targetObject, targetProperty) ? 1 : 0;
    }
}
