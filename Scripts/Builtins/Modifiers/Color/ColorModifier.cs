using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    [ModifierPropertyDefinition(typeof(ColorProperty))]
    public abstract class ColorModifier<T> : ComponentModifier<T> where T : Component
    {
        protected abstract void SetColor(Color color);

        public override void Update()
        {
            base.Update();

            if (!transitionChanged)
                return;

            float r = 0, g = 0, b = 0, a = 0;
            foreach (var kv in transitionState)
            {
                var property = GetProperty(kv.Key) as ColorProperty;
                var value = kv.Value;

                r += property.color.r * value;
                g += property.color.g * value;
                b += property.color.b * value;
                a += property.color.a * value;
            }

            SetColor(new Color(r, g, b, a));
        }
    }

    [Serializable]
    public class ColorProperty : Modifier.PropertyBase
    {
        // custom params
        public Color color;
    }
}
