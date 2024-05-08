// #define BINDING_DEEP_PROFILE

using UnityEngine;
using System;
using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
    [Obsolete, Preserve]
    public class UnityObjectField : UpdateableField
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

        public override void Update()
        {
            #if BINDING_DEEP_PROFILE
            using var _ = new ScopedProfile($"UnityObjectField.Update {targetObject.name}.{targetProperty}");
            #endif
            
            if (objectCtx == null || (targetObject is MonoBehaviour mb && !mb.isActiveAndEnabled))
                SetValue(negate ? 1 : 0);

            else
            {
                var v = objectCtx.Boolean_GetValue() ? 1 : 0;
                SetValue(negate ? (v + 1) % 2 : v);
            }
            
            SetPendingUpdate();
        }
    }
}
