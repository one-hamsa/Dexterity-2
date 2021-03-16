using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class ImageColorModifier : ColorModifier
    {
        Image image;
        protected override void Start()
        {
            base.Start();

            image = GetComponent<Image>();
        }

        protected override void SetColor(Color color) => image.color = color;
    }
}
