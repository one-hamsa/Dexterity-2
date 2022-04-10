using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
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

        public ListSet<string> stateNames = new ListSet<string>(32);
        public string[] fieldNames;

        private HashSet<StateFunction> stateFunctions = new HashSet<StateFunction>();

        /// <summary>
        /// returns the field ID, useful for quickly getting the field definition.
        /// only use on Awake, never on Update.
        /// </summary>
        /// <param name="name">Field name</param>
        /// <returns>Field Definition ID (runtime, may vary from run to run)</returns>
        public int GetFieldID(string name)
        {
            if (fieldNames.Length == 0)
                Debug.LogWarning($"tried to get field id of {name} but fieldNames is empty");

            return Array.IndexOf(fieldNames, name);
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
        /// Registers a state function, adding global state IDs
        /// </summary>
        /// <param name="stateFunction">State Function asset to register</param>
        /// <returns>State Function runtime instance</returns>
        public void RegisterStateFunction(StateFunction asset)
        {
            stateFunctions.Add(asset);

            foreach (var state in StateFunction.GetStates(asset))
                stateNames.Add(state);
        }

        /// <summary>
        /// Builds cache for runtime uses
        /// </summary>
        private void Initialize()
        {
            fieldNames = new string[settings.fieldDefinitions.Length];
            for (var i = 0; i < settings.fieldDefinitions.Length; ++i)
                fieldNames[i] = settings.fieldDefinitions[i].name;
        }

        /// <summary>
        /// Uninitializes state (useful for editor)
        /// </summary>
        private void Uninitialize() {
            fieldNames = null;
            stateNames.Clear();
            stateFunctions.Clear();
        }
    }
}
