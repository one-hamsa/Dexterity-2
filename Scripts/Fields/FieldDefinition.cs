using System;
using System.Linq;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    [Serializable]
    public struct FieldDefinition
    {
        [SerializeField]
        private string name;
        public FieldNode.FieldType type;
        public string[] enumValues;
        [NonSerialized] public bool isInternal;
        
        public string GetName()
        {
            return isInternal ? $".{name}" : name;
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

        #if UNITY_EDITOR
        public void SetName_Editor(string s)
        {
            name = s;
        }
        #endif
    }
}
