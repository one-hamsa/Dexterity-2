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
        public List<StateFunctionGraph> stateFunctions;
        public GlobalFloatValue[] globalFloatValues;

        public float GetGlobalFloat(string name, float defaultValue = default)
        {
            foreach (var g in globalFloatValues)
                if (g.name == name)
                    return g.value;

            return defaultValue;
        }
    }
}