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

        Renderer GetRenderer() {
            if (rend == null)
                rend = GetComponent<Renderer>();
            return rend;
        }
        Renderer rend;
        protected void Start()
        {
            // cache
            GetRenderer();
        }

        protected override void SetColor(Color color)
        {
            var name = materialColorName;
            if (string.IsNullOrEmpty(name))
                name = "_Color";

            GetRenderer().material.SetColor(name, color);
        }
    }
}
