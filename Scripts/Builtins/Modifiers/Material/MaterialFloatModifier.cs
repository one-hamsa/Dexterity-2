using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    public class MaterialFloatModifier : BaseMaterialModifier, ISupportPropertyFreeze
    {
        public string propertyName = "";
        public float minValue = 0f;
        public float maxValue = 1f;

        public class Property : PropertyBase
        {
            // custom params
            public float value = 0f;
        }
        
        private int propertyId;
        public override void Awake()
        {
            base.Awake();
            propertyId = Shader.PropertyToID(propertyName);
        }

        // Update is called once per frame
        public override void Update()
        {
            base.Update();

            if (!transitionChanged)
                return;
            
            float total = 0f;
            foreach (var kv in transitionState)
            {
                var property = (Property)GetProperty(kv.Key);
                var value = kv.Value;

                total += property.value * value;
            }
            targetMaterial.SetFloat(propertyId, Mathf.Lerp(minValue, maxValue, total));
        }

        void ISupportPropertyFreeze.FreezeProperty(PropertyBase property)
        {
            #if UNITY_EDITOR
            var prop = (Property)property;

            var allProps = UnityEditor.MaterialEditor.GetMaterialProperties(new [] { actions.getSharedMaterial(component) });
                if (!allProps.Select(p => p.name).Contains(propertyName))
                    return;
                    
            prop.value = actions.getSharedMaterial(component).GetFloat(propertyName);
            #endif
        }
    }
}
