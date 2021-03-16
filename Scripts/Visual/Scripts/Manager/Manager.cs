using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    [AddComponentMenu("Dexterity/Visual/Dexterity Visual - Manager")]
    public class Manager : MonoBehaviour
    {
        [Serializable]
        public struct FieldDefinition
        {
            public string Name;
            public Node.FieldType Type;
            public string[] EnumValues;
        }

        [SerializeField]
        private FieldDefinition[] fieldDefinitions;
        [SerializeField]
        private List<StateFunction> stateFunctions;

        public FieldDefinition[] FieldDefinitions => fieldDefinitions;
        Dictionary<string, FieldDefinition> cachedFieldDefs = null;
        public FieldDefinition? GetFieldDefinition(string name)
        {
            if (cachedFieldDefs != null)
            {
                // runtime
                if (cachedFieldDefs.TryGetValue(name, out var value))
                    return value;
            }
            else
            {
                // editor realtime data
                foreach (var df in fieldDefinitions)
                    if (df.Name == name)
                        return df;
            }

            Debug.LogWarning($"No field definition for {name}");
            return null;
        }
        public StateFunction[] StateFunctions => stateFunctions.Where(sf => sf != null).ToArray();
        public StateFunction GetStateFunction(string name) => stateFunctions
            .Where(sf => sf != null && sf.name == name)
            .First();

        private static bool destroyed;

        // TODO improve singleton implementation (spawn first, die last)
        private static Manager instance;
        public static Manager Instance
        {
            get
            {
                if (instance == null && !destroyed)
                {
                    instance = FindObjectOfType<Manager>();
                    if (instance == null)
                    {
                        // manager is already dead
                        return null;
                    }
                }
                return instance;
            }
        }

        protected Graph graph = new Graph();
        public void RegisterField(BaseField field) => graph.AddNode(field);
        public void UnregisterField(BaseField field) => graph.RemoveNode(field);

        public void Awake()
        {
            foreach (var sf in stateFunctions.ToArray()) // clone to modify original
            {
                sf.Initialize();
                if (!sf.Validate())
                {
                    Debug.LogError($"State function {sf.name} couldn't be validated, disabling. " +
                        $"This might cause unwanted effects");
                    stateFunctions.Remove(sf);
                }
            }

            BuildCache();
        }

        // Update is called once per frame
        void Update()
        {
            graph.Run();
        }

        void BuildCache()
        {
            cachedFieldDefs = fieldDefinitions.ToDictionary(fd => fd.Name);
        }
    }
}
