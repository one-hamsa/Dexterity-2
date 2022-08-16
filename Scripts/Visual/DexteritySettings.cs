using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    [CreateAssetMenu(menuName = "Dexterity/Settings", fileName = "Dexterity Settings")]
    public class DexteritySettings : ScriptableObject
    {
        [Serializable]
        public class GlobalFloatValue
        {
            public string name;
            public float value;
        }

        public FieldDefinition[] fieldDefinitions;

        [SerializeReference]
        public ITransitionStrategy defaultTransitionStrategy;

        public GlobalFloatValue[] globalFloatValues;

        public float GetGlobalFloat(string name, float defaultValue = default)
        {
            foreach (var g in globalFloatValues)
                if (g.name == name)
                    return g.value;

            return defaultValue;
        }

		[Tooltip("Min time in seconds between hitting the same receiver (0 to disable)")]
		public float repeatHitCooldown = 0f;
    }
}