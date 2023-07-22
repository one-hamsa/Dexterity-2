using System;

namespace OneHamsa.Dexterity
{
    [Serializable]
    public class EnumObjectBinding : ObjectBinding
    {
        public override ValueType supportedTypes => ValueType.Enum;
    }
}