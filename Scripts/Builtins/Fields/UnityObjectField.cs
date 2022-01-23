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
        public bool negate;

        ObjectValueAttribute.Context objectCtx;

        protected override void Initialize(Node context)
        {
            base.Initialize(context);

            objectCtx = ObjectValueAttribute.CreateContext(this, nameof(targetProperty));
        }

        public override int GetValue() 
        {
            if (objectCtx == null)
                return Node.defaultFieldValue;

            var value = objectCtx.GetValue<bool>() ? 1 : 0;
            return negate ? (value + 1) % 2 : value;
        }
    }
}
