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
        
        public ListSet<string> stateNames = new(32);

        /// <summary>
        /// returns the state ID, this is the state reference throughout the code.
        /// only use on Awake, never on Update.
        /// </summary>
        /// <param name="name">State name</param>
        /// <returns>State ID (runtime, may vary from run to run)</returns>
        public int GetStateID(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return StateFunction.emptyStateId;
            
            // lazy
            var indexOf = stateNames.IndexOf(name);
            if (indexOf == -1)
                RegisterState(name);

            return stateNames.IndexOf(name);
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

            if (id >= stateNames.Count)
            {
                Debug.LogError($"state id {id} is out of range");
                return null;
            }
            return stateNames[id];
        }

        /// <summary>
        /// Registers a step list, adding global state and field IDs
        /// </summary>
        /// <param name="stateFunction">Step List to register</param>
        public void Register(IHasStates stateHolder)
        {
            foreach (var state in stateHolder.GetStateNames())
                RegisterState(state);
        }


        /// <summary>
        /// Builds cache for runtime uses
        /// </summary>
        private void Initialize()
        {
            for (var i = 0; i < settings.fieldDefinitions.Length; ++i)
                RegisterField(settings.fieldDefinitions[i]);
        }

        private void RegisterField(string fieldName)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
            {
                Debug.LogError("tried to register empty field name");
                return;
            }
            stateNames.Add(fieldName);
        }

        private void RegisterState(string stateName)
        {
            if (string.IsNullOrWhiteSpace(stateName))
            {
                Debug.LogError("tried to register empty state name");
                return;
            }
            stateNames.Add(stateName);
        }

        /// <summary>
        /// Uninitializes state (useful for editor)
        /// </summary>
        private void Uninitialize() {
            stateNames.Clear();
        }

    }
}
