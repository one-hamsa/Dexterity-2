using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    public class EnumFieldAttribute : PropertyAttribute
    {
        public readonly string fieldName;

        public EnumFieldAttribute(string fieldName)
        {
            this.fieldName = fieldName;
        }
    }
}