using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    using Utilities;
    
    public class Database : IDisposable
    {
        public static Database instance { get; private set; }

        public readonly DexteritySettings settings;

        public static Database Create(DexteritySettings settings)
        {
            if (instance != null)
            {
                Debug.LogError("Database already exists");
                return null;
            }

            instance = new Database(settings);
            return instance;
        }
        public static void Destroy()
        {
            instance?.Dispose();
            instance = null;
        }
        
        private Database(DexteritySettings settings)
        {
            if (instance != null) {
                throw new Exception("Database already exists");
            }
            instance = this;

            this.settings = settings;
            settings.BuildCache();

            Initialize();
        }

        public void Dispose()
        {
            Uninitialize();
        }

        public double deltaTime => Mathf.Min(Time.maximumDeltaTime, Time.unscaledDeltaTime) * timeScale;
        public double timeScale = 1d;
        
        public ListSet<string> fieldNames = new(32);
        public ListSet<string> stateNames = new(32);
        public ListSet<string> internalFieldNames = new(32);
        public Dictionary<int, FieldDefinition> internalFields = new(32);

        private HashSet<IHasStates> stateHolders = new();

        /// <summary>
        /// returns the field ID, useful for quickly getting the field definition.
        /// only use on Awake, never on Update.
        /// </summary>
        /// <param name="name">Field name</param>
        /// <returns>Field Definition ID (runtime, may vary from run to run)</returns>
        public int GetFieldID(string name)
        {
            if (fieldNames.Count == 0)
                Debug.LogWarning($"tried to get field id of {name} but fieldNames is empty");

            var id = fieldNames.IndexOf(name);
            if (id == -1)
            {
                // try to find in internal
                var internalId = internalFieldNames.IndexOf(name);
                if (internalId != -1)
                    // internal fields are negative and start from -2
                    id = -2 - internalId;
            }
            return id;
        }

        /// <summary>
        /// returns the state ID, this is the state reference throughout the code.
        /// only use on Awake, never on Update.
        /// </summary>
        /// <param name="name">State name</param>
        /// <returns>State ID (runtime, may vary from run to run)</returns>
        public int GetStateID(string name)
        {
            if (stateNames.Count == 0)
                throw new Exception($"tried to get state id of {name} but stateNames is empty");

            return stateNames.IndexOf(name);
        }

        /// <summary>
        /// returns field definition by ID - fast.
        /// </summary>
        /// <param name="id">Field Definition ID</param>
        /// <returns>corresponding Field Definition</returns>
        public FieldDefinition GetFieldDefinition(int id)
        {
            if (id == -1)
            {
                throw new Exception("asked for field id == -1");
            }
            if (id < -1)
            {
                // internal field
                if (!internalFields.TryGetValue(id, out var field))
                    throw new Exception($"internal field {id} not found");
                return field;
            }
            return settings.fieldDefinitions[id];
        }

        /// <summary>
        /// returns the state string from an ID (runtime).
        /// </summary>
        /// <param name="name">State ID</param>
        /// <returns>State name</returns>
        public string GetStateAsString(int id)
        {
            if (id == -1)
                // special case for -1, which is the empty state
                return null;

            return stateNames[id];
        }

        /// <summary>
        /// Registers a step list, adding global state and field IDs
        /// </summary>
        /// <param name="stateFunction">Step List to register</param>
        public void Register(IHasStates stateHolder)
        {
            stateHolders.Add(stateHolder);

            foreach (var fieldName in stateHolder.GetFieldNames())
            {
                if (!FieldDefinition.IsInternalName(fieldName))
                    RegisterField(fieldName);
            }

            foreach (var state in stateHolder.GetStateNames())
                RegisterState(state);
        }

        /// <summary>
        /// Registers an internal field, adding it to the internal field list
        /// </summary>
        /// <param name="fieldDefinition"></param>
        public void RegisterInternalFieldDefinition(FieldDefinition fieldDefinition)
        {
            internalFieldNames.Add(fieldDefinition.GetInternalName());
            
            var fieldId = GetFieldID(fieldDefinition.GetInternalName());
            if (internalFields.TryGetValue(fieldId, out var existingField))
            {
                if (!existingField.Equals(fieldDefinition))
                {
                    Debug.LogError($"internal field {fieldDefinition.GetInternalName()} already exists " +
                                   $"with different definition, this is currently not supported");
                }
            }
            else
            {
                internalFields.Add(fieldId, fieldDefinition);
            }
        }

        /// <summary>
        /// Builds cache for runtime uses
        /// </summary>
        private void Initialize()
        {
            for (var i = 0; i < settings.fieldDefinitions.Length; ++i)
                RegisterField(settings.fieldDefinitions[i].name);
        }

        private void RegisterField(string fieldName) => fieldNames.Add(fieldName);
        private void RegisterState(string stateName) => stateNames.Add(stateName);

        /// <summary>
        /// Uninitializes state (useful for editor)
        /// </summary>
        private void Uninitialize() {
            fieldNames = null;
            internalFieldNames.Clear();
            stateNames.Clear();
            stateHolders.Clear();
        }

    }
}
