using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class MaterialColorModifier : ColorModifier
    {
        public string materialColorName = "_Color";

        Renderer rend;
        protected void Start()
        {
            rend = GetComponent<Renderer>();
        }

        protected override void SetColor(Color color)
        {
            var name = materialColorName;
            if (string.IsNullOrEmpty(name))
                name = "_Color";

            rend.material.SetColor(name, color);
        }
    }
}
