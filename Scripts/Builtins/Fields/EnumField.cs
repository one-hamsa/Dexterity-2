using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace OneHamsa.Dexterity.Builtins
{
    public class EnumField : BaseField
    {
        public BindingEnumNode targetNode;
        [EnumField(nameof(targetNode))]
        public string targetField;
        public bool negate;

        private int cachedEnumValue;

        public override int GetValue()
        {
            var value = targetNode.GetEnumValue() == cachedEnumValue ? 1 : 0;
            return negate ? (value + 1) % 2 : value;
        }

        protected override void Initialize(FieldNode context)
        {
            base.Initialize(context);

            // initialize just to be sure
            targetNode.InitializeBinding();
            cachedEnumValue = Convert.ToInt32(Enum.Parse(targetNode.targetEnumType, targetField));
        }
    }
}
