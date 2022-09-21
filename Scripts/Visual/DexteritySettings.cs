using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
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
        
        public ITransitionStrategy CreateDefaultTransitionStrategy()
        {
            if (defaultTransitionStrategy == null)
            {
                Debug.LogError($"No default transition strategy set in {name}", this);
                return null;
            }
            return DeepClone(defaultTransitionStrategy);
        }

		[Tooltip("Min time in seconds between hitting the same receiver (0 to disable)")]
		public float repeatHitCooldown = 0f;
        
        // Return a deep clone of an object of type T.
        private static T DeepClone<T>(T obj)
        {
            using (MemoryStream memory_stream = new MemoryStream())
            {
                // Serialize the object into the memory stream.
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(memory_stream, obj);

                // Rewind the stream and use it to create a new object.
                memory_stream.Position = 0;
                return (T)formatter.Deserialize(memory_stream);
            }
        }
    }
}