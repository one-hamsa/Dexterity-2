using System;

namespace OneHamsa.Dexterity
{
    [Serializable]
    public struct FieldDefinition
    {
        public string name;
        public Node.FieldType type;
        public string[] enumValues;
    }
}
