using System;
using System.Linq;

namespace OneHamsa.Dexterity
{
    [Serializable]
    public struct FieldDefinition
    {
        public string name;
        public FieldNode.FieldType type;
        public string[] enumValues;
        
        public string GetInternalName()
        {
            return $".{name}";
        }

        public static bool IsInternalName(string fieldName) => fieldName.StartsWith(".");

        public bool Equals(FieldDefinition other)
        {
            return name == other.name && type == other.type && enumValues.ToList().SequenceEqual(other.enumValues);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(name, (int)type, enumValues);
        }
    }
}
