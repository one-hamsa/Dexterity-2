using UnityEngine;
using System;
using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
    [Preserve]
    public class EnumField : BaseField
    {
        public BindingEnumNode targetNode;
        [EnumField(nameof(targetNode))]
        public string targetField;
        public bool negate;

        private int cachedEnumValue;

        public override bool GetValue()
        {
            var value = targetNode != null && targetNode.GetEnumValue() == cachedEnumValue;
            return negate ? !value : value;
        }

        protected override void Initialize(FieldNode context)
        {
            base.Initialize(context);

            if (targetNode == null)
            {
                Debug.LogError($"[{context.name}] EnumField: targetNode is null", context);
                return;
            }

            // initialize just to be sure
            targetNode.InitializeBinding();
            cachedEnumValue = Convert.ToInt32(Enum.Parse(targetNode.bindingType, targetField));
        }
    }
}
