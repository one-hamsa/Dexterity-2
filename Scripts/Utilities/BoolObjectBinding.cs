using System;

namespace OneHamsa.Dexterity
{
    [Serializable]
    public class BoolObjectBinding : ObjectBinding
    {
        public override ValueType supportedTypes => ValueType.Boolean;
    }
}