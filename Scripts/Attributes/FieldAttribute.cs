using UnityEngine;

namespace OneHamsa.Dexterity
{
    public class FieldAttribute : PropertyAttribute
    {
        public bool allowNull;

        public FieldAttribute(bool allowNull = false)
        {
            this.allowNull = allowNull;
        }
    }
}