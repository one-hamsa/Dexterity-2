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
        Image GetImage() {
            if (image == null)
                image = GetComponent<Image>();
            return image;
        }
        Image image;
        protected void Start()
        {
            // cache
            GetImage();
        }

        protected override void SetColor(Color color) => GetImage().color = color;


        public void FreezeProperty(PropertyBase property)
        {
            var prop = property as Property;
            prop.color = GetImage().color;
        }
    }
}  
