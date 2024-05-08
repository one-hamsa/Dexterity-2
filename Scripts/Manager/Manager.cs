using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

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
                    // This seems to catch the 'tear-down' state of the editor and avoid the long delay when quitting play-mode
                    // (because every modifier/field tries to access Manager.instance and Unity decided it is now null) 
                    #if UNITY_EDITOR
                    if (EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
                        return null;
                    #endif
                    
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
        }
        
        protected void OnDestroy()
        {
            Database.Destroy();
        }
        
        protected void Update()
        {
            using var _ = new ScopedProfile("Dexterity: Update Modifiers");
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
        }
    }
}
