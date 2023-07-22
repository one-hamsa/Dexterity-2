using System;

namespace OneHamsa.Dexterity
{
    [Serializable]
    public class EnumOrBoolObjectBinding : ObjectBinding
    {
        public override ValueType supportedTypes => ValueType.Boolean | ValueType.Enum;
    }
}