using UnityEngine;

namespace OneHamsa.Dexterity
{
    public class FieldValueAttribute : PropertyAttribute
    {
        public string fieldName;
        public bool proxy;

        public FieldValueAttribute(string fieldName, bool proxy = false)
        {
            this.fieldName = fieldName;
            this.proxy = proxy;
        }
    }
}