using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class EnumNodeField : BaseField
    {
        public EnumNode targetNode;
        [EnumField(nameof(targetNode))]
        public string targetField;
        public bool negate;

        public override int GetValue()
        {
            var value = targetNode.GetEnumValue() == targetField ? 1 : 0;
            return negate ? (value + 1) % 2 : value;
        }
    }
}
