using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    [AddComponentMenu("Dexterity/Dexterity Manager")]
    [DefaultExecutionOrder(Manager.managerExecutionPriority)]
    public class Manager : MonoBehaviour
    {
        internal const int managerExecutionPriority = -20;
        internal const int nodeExecutionPriority = -15;
        internal const int modifierExecutionPriority = -10;

        public DexteritySettings settings;

        public ListSet<string> stateNames = new ListSet<string>(32);
        [NonSerialized]
        public string[] fieldNames;
        private Dictionary<StateFunctionGraph, StateFunctionGraph> stateFunctions
            = new Dictionary<StateFunctionGraph, StateFunctionGraph>();

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
                Debug.LogWarning($"tried to get state id of {name} but stateNames is empty");

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
            if (id == -1)
                // special case for -1, which is the empty state
                return null;

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
        /// Registers a state function, adding global state IDs
        /// </summary>
        /// <param name="stateFunction">State Function asset to register</param>
        /// <returns>State Function runtime instance</returns>
        public StateFunctionGraph RegisterStateFunction(StateFunctionGraph asset)
        {
            if (!stateFunctions.TryGetValue(asset, out var runtime)) {
                stateFunctions[asset] = runtime = Instantiate(asset);

                // first add the states so Initialize() can use them
                foreach (var state in runtime.GetStates())
                    stateNames.Add(state);

                // then initialize all nodes
                runtime.Initialize();
            }
            return runtime;
        }

        /// <summary>
        /// Resets manager state (useful for editor)
        /// </summary>
        public void Reset() {
            fieldNames = null;
            stateNames.Clear();
            
            foreach (var sf in stateFunctions.Values)
                Destroy(sf);
            stateFunctions.Clear();
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
