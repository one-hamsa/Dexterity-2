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
        public class SavedProperty
        {
            public string name;
            [SerializeReference] 
            public Modifier.PropertyBase property;
        }

        public FieldDefinition[] fieldDefinitions;

        [SerializeReference]
        public ITransitionStrategy defaultTransitionStrategy;
        public List<SavedProperty> namedProperties = new();

        private Dictionary<(Type, string), Modifier.PropertyBase> namedPropertiesCache = new();

        public ITransitionStrategy CreateDefaultTransitionStrategy()
        {
            if (defaultTransitionStrategy == null)
            {
                Debug.LogError($"No default transition strategy set in {name}", this);
                return null;
            }
            return defaultTransitionStrategy.Clone();
        }
        
        public void SavePropertyAs(Modifier.PropertyBase property, string name)
        {
            var newProperty = property.Clone();
            property.savedPropertyKey = name;
            namedProperties.Add(new SavedProperty
            {
                name = name,
                property = newProperty
            });
        }

        public void BuildCache()
        {
            namedPropertiesCache.Clear();
            foreach (var savedProperty in namedProperties)
            {
                namedPropertiesCache[(savedProperty.property.GetType(), savedProperty.name)] 
                    = savedProperty.property;
            }
        }
        
        public Modifier.PropertyBase GetSavedProperty(Type type, string name)
        {
            if (namedPropertiesCache.TryGetValue((type, name), out var property))
            {
                return property;
            }
            return null;
        }
        
        public IEnumerable<string> GetSavedPropertiesForType(Type type)
        {
            foreach (var (t, name) in namedPropertiesCache.Keys)
            {
                if (t == type)
                    yield return name;
            }
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