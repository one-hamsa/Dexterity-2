using System;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    public class MaterialFloatModifier : BaseMaterialModifier, ISupportPropertyFreeze
    {
        public string propertyName = "";

        [Serializable]
        public class Property : PropertyBase
        {
            // custom params
            public float value = 0f;
        }
        
        private int propertyId;

        protected override void Awake()
        {
            base.Awake();
            CachePropertyID();
        }

        private void CachePropertyID()
        {
            propertyId = Shader.PropertyToID(propertyName);
        }

        // Update is called once per frame
        public override void Refresh()
        {
            base.Refresh();

            if (!transitionChanged)
                return;
            
            float total = 0f;
            foreach (var kv in transitionState.keyValuePairs)
            {
                var property = (Property)GetProperty(kv.Key);
                var value = kv.Value;

                total += property.value * value;
            }
            SetFloat(propertyId, total);
        }

        void ISupportPropertyFreeze.FreezeProperty(PropertyBase property)
        {
            var prop = (Property)property;
            
            CachePropertyID();
            prop.value = GetFloat(propertyId);
        }

        #if UNITY_EDITOR
        public override (string, LogType) GetEditorComment()
        {
            if (string.IsNullOrEmpty(propertyName))
                return ("Property name is empty", LogType.Error);
            
            CachePropertyID();
            CacheComponent();
            if (!propertyTypes.ContainsKey(propertyId))
                return ($"Property {propertyName} not found in material", LogType.Error);
            
            return base.GetEditorComment();
        }
        #endif
    }
}
