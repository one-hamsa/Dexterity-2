using UnityEngine;
using System;
using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
    [Preserve]
    [System.Serializable]
    public class EnumField : UpdateableField
    {
        public BindingEnumNode targetNode;
        [EnumField(nameof(targetNode))]
        public string targetField;
        public bool negate;

        private int cachedEnumValue;

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

        public override void Update()
        {
            var v = targetNode.initialized && targetNode.GetEnumValue() == cachedEnumValue ? 1 : 0;
            SetValue(negate ? (v + 1) % 2 : v);
            
            SetPendingUpdate();
        }
    }
}
