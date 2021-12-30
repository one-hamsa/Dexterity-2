using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class SpriteColorModifier : ColorModifier
    {
        SpriteRenderer GetRenderer() {
            if (rend == null)
                rend = GetComponent<SpriteRenderer>();
            return rend;
        }
        SpriteRenderer rend;
        protected void Start()
        {
            // cache
            GetRenderer();
        }

        protected override void SetColor(Color color) => GetRenderer().color = color;
    }
}
