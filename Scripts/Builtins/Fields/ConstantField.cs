using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
    [Preserve]
    public class ConstantField : BaseField
    {
        public bool constant;

        public override bool GetValue() => constant;
    }
}
