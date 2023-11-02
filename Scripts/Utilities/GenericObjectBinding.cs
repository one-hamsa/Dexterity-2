using System;

namespace OneHamsa.Dexterity
{
    [Serializable]
    public class GenericObjectBinding : ObjectBinding
    {
        public override ValueType supportedTypes => ValueType.Boolean | ValueType.Int | ValueType.Enum;
    }
}