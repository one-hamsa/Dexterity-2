using UnityEngine;

namespace OneHamsa.Dexterity.Visual
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