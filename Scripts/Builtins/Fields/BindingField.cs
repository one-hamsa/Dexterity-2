// #define BINDING_DEEP_PROFILE

using System;
using System.Data;
using UnityEngine;
using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
    [Preserve]
    public class BindingField : UpdateableField
    {
        public BoolObjectBinding binding = new();
        public bool negate;

        public override BaseField CreateDeepClone()
        {
            throw new DataException("Attempting to DeepClone a bindingField (how can we bind from an asset?)");
        }

        protected override void Initialize(FieldNode context)
        {
            base.Initialize(context);

            if (!binding.IsValid())
                return;

            if (binding.IsInitialized())
                return;

            if (!binding.Initialize())
            {
                Debug.LogError($"{nameof(BindingField)}: Failed to initialize binding for {context}", context);
                return;
            }
        }

        public override void Update()
        {
            #if BINDING_DEEP_PROFILE
            using var _ = new ScopedProfile($"BindingField.Update {binding.target.name}.{binding.methodName}");
            #endif

            try
            {
                if (!binding.IsInitialized() || binding.target is MonoBehaviour { isActiveAndEnabled: false })
                    SetValue(negate ? 1 : 0);
                else
                {
                    var v = binding.Boolean_GetValue() ? 1 : 0;
                    SetValue(negate ? (v + 1) % 2 : v);
                }

                // always set pending update, we can't know when the binding will change
                SetPendingUpdate();
            }
            catch (MissingReferenceException)
            {
                // target was destroyed, it's ok, stop updating
                SetValue(negate ? 1 : 0);
                Finalize(context);
            } 
            catch (NullReferenceException) 
            {
                if (binding != null && binding.target == null) {
                    // target was destroyed, it's ok, stop updating
                    SetValue(negate ? 1 : 0);
                    Finalize(context);
                } else {
                    throw;
                }
            }
        }
    }
}
