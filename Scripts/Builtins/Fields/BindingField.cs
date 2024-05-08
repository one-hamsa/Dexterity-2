using System.Collections.Generic;
using System.Data;
using OneHamsa.Dexterity.Utilities;
using UnityEngine;
using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
    [Preserve]
    public class BindingField : BaseField
    {
        public class BindingFieldProvider : MonoBehaviour
        {
            internal BindingField field;

            private void Update()
            {
                if (!field.binding.IsValid() || !field.binding.IsInitialized())
                    field.SetValue(field.negate ? 1 : 0);
                else
                {
                    var v = field.binding.Boolean_GetValue() ? 1 : 0;
                    field.SetValue(field.negate ? (v + 1) % 2 : v);
                }
            }
        }
        
        public BoolObjectBinding binding;
        public bool negate;
        private BindingFieldProvider provider;

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
            
            provider = context.gameObject.GetOrAddComponent<BindingFieldProvider>();
            provider.field = this;
        }

        public override void Finalize(FieldNode context)
        {
            base.Finalize(context);
            
            if (provider != null)
                UnityEngine.Object.Destroy(provider);
        }
    }
}
