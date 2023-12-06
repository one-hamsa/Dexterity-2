using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.Events;
using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
    [Obsolete, Preserve]
    public class UnityObjectField : BaseField
    {
        public UnityEngine.Object targetObject;
        [ObjectValue(objectFieldName: nameof(targetObject), ObjectValueContext.ValueType.Boolean)]
        public string targetProperty;
        public bool negate;

        ObjectValueContext objectCtx;

        protected override void Initialize(FieldNode context)
        {
            base.Initialize(context);

            if (targetObject == null)
                return;
            
            try
            {
                objectCtx ??= new ObjectValueContext(this, nameof(targetProperty));
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

            var value = objectCtx.Boolean_GetValue() ? 1 : 0;
            return negate ? (value + 1) % 2 : value;
        }
    }
}
