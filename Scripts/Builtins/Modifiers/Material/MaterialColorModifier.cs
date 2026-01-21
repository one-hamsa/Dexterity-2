using System;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    public class MaterialColorModifier : BaseMaterialModifier, ISupportPropertyFreeze
    {
        public string materialColorName = "_Color";

        [Serializable]
        public class Property : PropertyBase
        {
            // custom params
            public Color color;
        }
        
        private int propertyId;

        protected override void Awake()
        {
            base.Awake();
            CachePropertyID();
        }

        private void CachePropertyID()
        {
            propertyId = Shader.PropertyToID(materialColorName);
        }

        // Update is called once per frame
        public override void Refresh()
        {
            base.Refresh();

            if (!transitionChanged)
                return;
            
            float r = 0, g = 0, b = 0, a = 0;
            foreach (var kv in transitionState.keyValuePairs)
            {
                var property = GetProperty(kv.Key) as Property;
                var value = kv.Value;

                r += property.color.r * value;
                g += property.color.g * value;
                b += property.color.b * value;
                a += property.color.a * value;
            }
            SetColor(propertyId, new Color(r, g, b, a));
        }

        public void FreezeProperty(PropertyBase property)
        {
            var prop = (Property)property;
            
            CachePropertyID();
            prop.color = GetColor(propertyId);
        }
        
        #if UNITY_EDITOR
        public override (string, LogType) GetEditorComment()
        {
            if (string.IsNullOrEmpty(materialColorName))
                return ("Property name is empty", LogType.Error);
            
            CachePropertyID();
            CacheComponent();
            if (!propertyTypes.ContainsKey(propertyId))
                return ($"Property {materialColorName} not found in material", LogType.Error);
            
            return base.GetEditorComment();
        }
        #endif
    }
}
