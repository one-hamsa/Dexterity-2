using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using OneHamsa.Dexterity.Builtins;
using UnityEngine;

namespace OneHamsa.Dexterity
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

        public SimpleStrategy defaultTransitionStrategy;
        public List<SavedProperty> namedProperties = new();

        private Dictionary<(Type, string), Modifier.PropertyBase> namedPropertiesCache = new();

        public ITransitionStrategy CreateDefaultTransitionStrategy()
        {
            if (defaultTransitionStrategy == null)
            {
                Debug.LogError($"No default transition strategy set in {name}", this);
                return null;
            }

            return SimpleStrategy.CloneFrom(defaultTransitionStrategy);
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
    }
}