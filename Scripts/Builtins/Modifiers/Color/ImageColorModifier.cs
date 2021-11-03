using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    [RequireComponent(typeof(Image))]
    public class ImageColorModifier : ColorModifier, ISupportPropertyFreeze
    {
        Image image;
        protected void Start()
        {
            image = GetComponent<Image>();
        }

        protected override void SetColor(Color color) => image.color = color;


        public void FreezeProperty(PropertyBase property)
        {
            var prop = property as Property;
            var image = GetComponent<Image>();
            prop.color = image.color;
        }
    }
}
