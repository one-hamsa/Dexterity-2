using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class MaterialColorModifier : ColorModifier
    {
        public string MaterialColorName = "_Color";

        Renderer rend;
        protected override void Start()
        {
            base.Start();

            rend = GetComponent<Renderer>();
        }

        protected override void SetColor(Color color)
        {
            var name = MaterialColorName;
            if (string.IsNullOrEmpty(name))
                name = "_Color";

            rend.material.SetColor(name, color);
        }
    }
}
