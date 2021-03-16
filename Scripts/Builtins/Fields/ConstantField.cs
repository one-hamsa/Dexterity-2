using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class ConstantField : BaseField
    {
        public int Constant;

        public override int GetValue() => Constant;
    }
}
