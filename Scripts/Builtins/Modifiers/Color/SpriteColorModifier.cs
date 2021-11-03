using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class SpriteColorModifier : ColorModifier
    {
        SpriteRenderer rend;
        protected void Start()
        {
            rend = GetComponent<SpriteRenderer>();
        }

        protected override void SetColor(Color color) => rend.color = color;
    }
}
