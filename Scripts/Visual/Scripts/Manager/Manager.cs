using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    [AddComponentMenu("Dexterity/Visual/Dexterity Visual - Manager")]
    public class Manager : MonoBehaviour
    {
        internal const int nodeExecutionPriority = 10;
        internal const int modifierExecutionPriority = 15;

        public FieldDefinition[] fieldDefinitions;

        public List<StateFunctionGraph> stateFunctions;

        public StateFunctionGraph[] activeStateFunctions { get; private set; }

        private string[] fieldNames;
        private List<string> stateNames;

        /**
         * returns the field ID, useful for quickly getting the field definition.
         * only use on Awake, never on Update.
         */
        public int GetFieldID(string name)
        {
            return Array.IndexOf(fieldNames, name);
        }

        /**
         * returns the state ID, this is the state reference throughout the code.
         * only use on Awake, never on Update.
         */
        public int GetStateID(string name)
        {
            return stateNames.IndexOf(name);
        }

        /**
         * returns field definition by name - slow.
         */
        public FieldDefinition GetFieldDefinitionByName(string name)
        {
            if (!Application.isEditor)
            {
                Debug.LogError("GetFieldDefinitionByName should only be called from editor");
            }

            foreach (var fd in fieldDefinitions)
                if (fd.name == name)
                    return fd;

            return default;
        }

        internal StateFunctionGraph GetActiveStateFunction(StateFunctionGraph stateFunction)
        {
            var index = stateFunctions.IndexOf(stateFunction);
            if (index == -1)
                return null;
            return activeStateFunctions[index];
        }

        /**
* returns state function by name - slow.
*/
        public StateFunctionGraph GetStateFunctionByName(string name) => stateFunctions
            .Where(sf => sf?.name == name)
            .First();

        /**
         * returns field definition by ID - quick.
         */
        public FieldDefinition GetFieldDefinition(int id)
        {
            return fieldDefinitions[id];
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

        public Graph graph { get; } = new Graph();
        public void RegisterField(BaseField field) => graph.AddNode(field);
        public void UnregisterField(BaseField field) => graph.RemoveNode(field);
        public void SetDirty() => graph.SetDirty();

        public void Awake()
        {
            // build cache first - important, builds runtime data structures
            BuildCache();

            // clone all state functions
            activeStateFunctions = new StateFunctionGraph[stateFunctions.Count];
            for (var i = 0; i < stateFunctions.Count; ++i)
            {
                activeStateFunctions[i] = Instantiate(stateFunctions[i]);
            }
        }
        public void Start()
        {
            // enable on start to let all nodes register to graph during OnEnable
            graph.started = true;
        }

        private void BuildCache()
        {
            fieldNames = new string[fieldDefinitions.Length];
            for (var i = 0; i < fieldDefinitions.Length; ++i)
                fieldNames[i] = fieldDefinitions[i].name;

            stateNames = new List<string>(32);
            foreach (var fn in stateFunctions)
            {
                if (fn == null)
                    continue;

                foreach (var state in fn.GetStates())
                    stateNames.Add(state);
            }
        }

        void Update()
        {
            graph.Run();
        }
    }
}
