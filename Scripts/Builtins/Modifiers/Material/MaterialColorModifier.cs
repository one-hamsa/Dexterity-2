using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    [RequireComponent(typeof(Renderer))]
    public class MaterialColorModifier : BaseMaterialModifier, ISupportPropertyFreeze
    {
        public string materialColorName = "_Color";

        public class Property : PropertyBase
        {
            // custom params
            public Color color;
        }

        // Update is called once per frame
        public override void Update()
        {
            base.Update();

            if (!transitionChanged)
                return;
            
            float r = 0, g = 0, b = 0, a = 0;
            foreach (var kv in transitionState)
            {
                var property = GetProperty(kv.Key) as Property;
                var value = kv.Value;

                r += property.color.r * value;
                g += property.color.g * value;
                b += property.color.b * value;
                a += property.color.a * value;
            }
            targetMaterial.SetColor(materialColorName, new Color(r, g, b, a));
        }

        void ISupportPropertyFreeze.FreezeProperty(PropertyBase property)
        {
            #if UNITY_EDITOR
            var prop = property as Property;

            var allProps = UnityEditor.MaterialEditor.GetMaterialProperties(new [] { component.sharedMaterial});
                if (!allProps.Select(p => p.name).Contains(materialColorName))
                    return;
                    
            prop.color = component.sharedMaterial.GetColor(materialColorName);
            #endif
        }
    }
}
