using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
    [Preserve, Serializable]
    public class ConstantField : BaseField
    {
        [FieldValue(nameof(BaseField.relatedFieldName), proxy = true)]
        public int constant;

        protected override void Initialize(FieldNode context)
        {
            base.Initialize(context);
            SetValue(constant);
        }
    }
}
