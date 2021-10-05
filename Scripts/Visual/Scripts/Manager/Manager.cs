using System;
using System.Collections.Generic;
using System.Linq;
using OneHumus.Data;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    [AddComponentMenu("Dexterity/Dexterity Manager")]
    public class Manager : MonoBehaviour
    {
        internal const int nodeExecutionPriority = -15;
        internal const int modifierExecutionPriority = -10;

        public DexteritySettings settings;

        public StateFunctionGraph[] activeStateFunctions { get; private set; }

        private string[] fieldNames;
        private ListSet<string> stateNames = new ListSet<string>(32);
        private ListSet<StateFunctionGraph> stateFunctions = new ListSet<StateFunctionGraph>(4);

        /// <summary>
        /// returns the field ID, useful for quickly getting the field definition.
        /// only use on Awake, never on Update.
        /// </summary>
        /// <param name="name">Field name</param>
        /// <returns>Field Definition ID (runtime, may vary from run to run)</returns>
        public int GetFieldID(string name)
        {
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
                Debug.LogError("asked for field id == -1", this);
                return default;
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
            return stateNames[id];
        }

        // TODO improve singleton implementation (spawn first, die last)
        private static Manager inst;
        public static Manager instance
        {
            get
            {
                if (inst == null)
                {
                    inst = FindObjectOfType<Manager>();
                    if (inst == null)
                    {
                        // manager is already dead
                        return null;
                    }
                }
                return inst;
            }
        }  

        public Graph graph { get; private set; }
        /// <summary>
        /// Registers a field to the graph.
        /// </summary>
        /// <param name="field">BaseField to register to the graph</param>
        public void RegisterField(BaseField field) => graph.AddNode(field);
        /// <summary>
        /// Removes a registered field from the graph.
        /// </summary>
        /// <param name="field">BaseField remove from the graph</param>
        public void UnregisterField(BaseField field) => graph.RemoveNode(field);
        /// <summary>
        /// Marks a field as dirty (forces re-sorting).
        /// </summary>
        /// <param name="field">BaseField to mark as dirty</param>
        public void SetDirty(BaseField field) => graph.SetDirty(field);

        /// <summary>
        /// Registers a state function, useful for managing global state IDs
        /// </summary>
        /// <param name="stateFunction">State Function to register</param>
        public void RegisterStateFunction(StateFunctionGraph stateFunction)
        {
            if (stateFunctions.Add(stateFunction))
            {
                foreach (var state in stateFunction.GetStates())
                    stateNames.Add(state);
            }
        }

        private void BuildCache()
        {
            fieldNames = new string[settings.fieldDefinitions.Length];
            for (var i = 0; i < settings.fieldDefinitions.Length; ++i)
                fieldNames[i] = settings.fieldDefinitions[i].name;
        }

        protected void Awake()
        {
            // build cache first - important, builds runtime data structures
            BuildCache();

            // create graph instance
            graph = gameObject.AddComponent<Graph>();
        }
        protected void Start()
        {
            // enable on start to let all nodes register to graph during OnEnable
            graph.started = true;
        }
    }
}
