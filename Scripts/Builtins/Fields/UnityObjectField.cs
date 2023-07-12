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

        protected override void Initialize(FieldNode context)
        {
            base.Initialize(context);

            objectCtx = null;
            if (targetObject == null)
                return;
            
            try
            {
                objectCtx = new ObjectBooleanContext(this, nameof(targetProperty));
            }
            catch (ArgumentException e)
            {
                Debug.LogException(e, context);
            }
        }

        public override int GetValue() 
        {
            if (objectCtx == null)
                return negate ? 1 : 0;

            var value = objectCtx.GetValue() ? 1 : 0;
            return negate ? (value + 1) % 2 : value;
        }
    }
}
