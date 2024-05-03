using System.Data;
using UnityEngine;
using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
    [Preserve]
    public class BindingField : BaseField
    {
        public BoolObjectBinding binding;
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
                Debug.LogError($"{nameof(BindingField)}: Failed to initialize binding for {context}", context);
        }

        public override void Finalize(FieldNode context)
        {
            base.Finalize(context);
            binding = null;
        }

        public override bool GetValue() 
        {
            if (!binding.IsValid() || !binding.IsInitialized())
                return negate;

            var value = binding.Boolean_GetValue();
            return negate ? !value : value;
        }
    }
}
