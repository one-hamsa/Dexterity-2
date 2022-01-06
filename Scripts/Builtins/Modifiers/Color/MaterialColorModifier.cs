using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class MaterialColorModifier : ColorModifier<Renderer>, ISupportPropertyFreeze
    {
        public string materialColorName = "_Color";
        protected string colorNameOrDefault 
        => !string.IsNullOrEmpty(materialColorName) ? materialColorName : "_Color";

        protected override void SetColor(Color color)
        {
            component.material.SetColor(colorNameOrDefault, color);
        }

        public void FreezeProperty(PropertyBase property)
        {
            var prop = property as ColorProperty;
            prop.color = component.material.GetColor(colorNameOrDefault);
        }
    }
}
