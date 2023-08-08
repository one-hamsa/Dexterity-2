using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace OneHamsa.Dexterity
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
        private HashSet<Modifier> modifiers = new();
        private List<Modifier> modifiersActiveList = new();

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
        public void AddModifier(Modifier modifier)
        {
            if (modifier == null)
            {
                Debug.LogError("Cannot add null modifier", modifier);
                return;
            }
            modifiers.Add(modifier);
        }

        /// <summary>
        /// Removes a modifier from the update pool
        /// </summary>
        /// <param name="modifier">modifier to remove</param>
        public void RemoveModifier(Modifier modifier) => modifiers.Remove(modifier);

        protected void Awake()
        {
            if (Database.instance == null)
                Database.Create(settings);
         
            // create graph instance
            graph = gameObject.AddComponent<Graph>();
        }
        
        protected void OnDestroy()
        {
            Database.Destroy();
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

            Profiler.BeginSample("Dexterity: Update Modifiers");
            // update all modifiers
            modifiersActiveList.Clear();
            modifiersActiveList.AddRange(modifiers);
            foreach (var modifier in modifiersActiveList)
            {
                try
                {
                    modifier.Refresh();
                }
                catch (Exception e)
                {
                    Debug.LogException(e, modifier);
                    // remove modifier from update pool if object is destroyed
                    if (modifier == null)
                    {
                        Debug.LogWarning($"Modifier of type {modifier.GetType().Name} was destroyed but not removed, " +
                                         $"the exception above was probably caused by that reason. " +
                                         $"removing from update pool");
                        modifiers.Remove(modifier);
                    }
                }
            }

            Profiler.EndSample();
        }
    }
}
