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

        ObjectValueAttribute cachedAttribute;

        protected override void Initialize(Node context)
        {
            base.Initialize(context);

            cachedAttribute = ObjectValueAttribute.Initialize(this, nameof(targetProperty));
        }

        public override int GetValue() => cachedAttribute.GetValue<bool>() ? 1 : 0;
    }
}
