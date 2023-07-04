using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace OneHamsa.Dexterity.Builtins
{
    public class ConstantField : BaseField
    {
        [FieldValue(nameof(BaseField.relatedFieldName), proxy = true)]
        public int constant;

        public override int GetValue() => constant;
    }
}
