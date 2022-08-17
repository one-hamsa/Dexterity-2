using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    using Utilities;
    
    public class Core
    {
        public static Core instance { get; private set; }

        public readonly DexteritySettings settings;

        public static Core Create(DexteritySettings settings)
        {
            if (instance != null)
            {
                Debug.LogError("Core already exists");
                return null;
            }

            instance = new Core(settings);
            return instance;
        }
        public static void Destroy()
        {
            instance = null;
        }
        
        private Core(DexteritySettings settings)
        {
            if (instance != null) {
                throw new Exception("Core already exists");
            }
            instance = this;

            this.settings = settings;

            Initialize();
        }

        ~Core() {
            Uninitialize();
        }

        public double deltaTime => Time.unscaledDeltaTime * timeScale;
        public double timeScale = 1d;
        
        public ListSet<string> fieldNames = new ListSet<string>(32);
        public ListSet<string> stateNames = new ListSet<string>(32);

        private HashSet<IHasStates> stateHolders = new HashSet<IHasStates>();

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

            return fieldNames.IndexOf(name);
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

            foreach (var field in stateHolder.GetFieldNames())
                RegisterField(field);

            foreach (var state in stateHolder.GetStateNames())
                RegisterState(state);
        }

        /// <summary>
        /// Builds cache for runtime uses
        /// </summary>
        private void Initialize()
        {
            for (var i = 0; i < settings.fieldDefinitions.Length; ++i)
                RegisterField(settings.fieldDefinitions[i].name);
        }

        private void RegisterField(string fieldName)
        {
            fieldNames.Add(fieldName);
        }

        private void RegisterState(string stateName)
        {
            stateNames.Add(stateName);
        }

        /// <summary>
        /// Uninitializes state (useful for editor)
        /// </summary>
        private void Uninitialize() {
            fieldNames = null;
            stateNames.Clear();
            stateHolders.Clear();
        }

    }
}
