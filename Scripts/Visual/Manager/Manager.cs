using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    [AddComponentMenu("Dexterity/Dexterity Manager")]
    [DefaultExecutionOrder(Manager.graphExecutionPriority)]
    public class Manager : MonoBehaviour
    {
        internal const int graphExecutionPriority = -20;
        internal const int nodeExecutionPriority = -15;
        internal const int modifierExecutionPriority = -10;


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

        public DexteritySettings settings;
        private List<Modifier> modifiers = new();

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
        /// Adds a modifier to the update pool
        /// </summary>
        /// <param name="modifier">modifier to add</param>
        public void AddModifier(Modifier modifier) => modifiers.Add(modifier);
        
        /// <summary>
        /// Removes a modifier from the update pool
        /// </summary>
        /// <param name="modifier">modifier to remove</param>
        public void RemoveModifier(Modifier modifier) => modifiers.Remove(modifier);

        protected void Awake()
        {
            if (Core.instance == null)
                Core.Create(settings);
         
            // create graph instance
            graph = gameObject.AddComponent<Graph>();
        }
        protected void Start()
        {
            // enable on start to let all nodes register to graph during OnEnable
            graph.started = true;
        }
        
        protected void Update()
        {
            // update graph
            graph.Refresh();
            
            // update all modifiers
            foreach (var modifier in modifiers)
                modifier.Refresh();
        }
    }
}
