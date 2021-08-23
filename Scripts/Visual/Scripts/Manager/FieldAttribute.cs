using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    public class FieldAttribute : PropertyAttribute
    {
        public bool drawLabelSeparately;

        public FieldAttribute(bool drawLabelSeparately = false)
        {
            this.drawLabelSeparately = drawLabelSeparately;
        }
    }
}