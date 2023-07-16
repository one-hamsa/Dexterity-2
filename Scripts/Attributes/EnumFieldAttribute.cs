using UnityEngine;

namespace OneHamsa.Dexterity
{
    public class EnumFieldAttribute : PropertyAttribute
    {
        public readonly string nodeFieldName;

        public EnumFieldAttribute(string nodeFieldName)
        {
            this.nodeFieldName = nodeFieldName;
        }
    }
}