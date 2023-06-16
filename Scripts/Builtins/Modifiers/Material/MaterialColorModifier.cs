using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    public class MaterialColorModifier : BaseMaterialModifier, ISupportPropertyFreeze
    {
        public string materialColorName = "_Color";

        public class Property : PropertyBase
        {
            // custom params
            public Color color;
        }
        
        private int propertyId;
        public override void Awake()
        {
            base.Awake();
            
            if (!targetMaterial.HasProperty(materialColorName))
            {
                Debug.LogError($"Property not found: {materialColorName} for material {targetMaterial.name}", this);
                if (Application.isPlaying)
                    enabled = false;
                return;
            }
            
            propertyId = Shader.PropertyToID(materialColorName);
        }

        // Update is called once per frame
        public override void Refresh()
        {
            base.Refresh();

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
            targetMaterial.SetColor(propertyId, new Color(r, g, b, a));
        }

        void ISupportPropertyFreeze.FreezeProperty(PropertyBase property)
        {
            #if UNITY_EDITOR
            var prop = property as Property;

            var allProps = UnityEditor.MaterialEditor.GetMaterialProperties(new [] { actions.getSharedMaterial(component) });
                if (!allProps.Select(p => p.name).Contains(materialColorName))
                    return;
                    
            prop.color = actions.getSharedMaterial(component).GetColor(materialColorName);
            #endif
        }
    }
}
