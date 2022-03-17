using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    [RequireComponent(typeof(Image))]
    public class ImageColorModifier : ColorModifier<Image>, ISupportPropertyFreeze
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
