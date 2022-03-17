using UnityEngine;
using TMPro;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    [RequireComponent(typeof(TMP_Text))]
    public class TMProColorModifier : ColorModifier<TMP_Text>, ISupportPropertyFreeze
    {
        protected override void SetColor(Color color) => component.color = color;

        public void FreezeProperty(PropertyBase property)
        {
            if (component == null)
                return;
                
            var prop = property as ColorProperty;
            prop.color = component.color;
        }
    }
}
