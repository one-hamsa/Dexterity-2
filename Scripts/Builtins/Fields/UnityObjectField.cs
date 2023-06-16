using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Events;

namespace OneHamsa.Dexterity.Builtins
{
    public class UnityObjectField : BaseField
    {
        public UnityEngine.Object targetObject;
        [ObjectValue(objectFieldName: nameof(targetObject), fieldType: typeof(bool))]
        public string targetProperty;
        public bool negate;

        ObjectBooleanContext objectCtx;

        protected override void Initialize(Node context)
        {
            base.Initialize(context);

            try
            {
                objectCtx = new ObjectBooleanContext(this, nameof(targetProperty));
            }
            catch (ArgumentException e)
            {
                Debug.LogException(e, context);
                objectCtx = null;
            }
        }

        public override int GetValue() 
        {
            if (objectCtx == null)
                return Node.defaultFieldValue;

            var value = objectCtx.GetValue() ? 1 : 0;
            return negate ? (value + 1) % 2 : value;
        }
    }
}
