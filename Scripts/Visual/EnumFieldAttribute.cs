using UnityEngine;

namespace OneHamsa.Dexterity.Visual
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