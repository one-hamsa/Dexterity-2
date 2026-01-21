using System;
using System.Collections.Generic;
using OneHamsa.Dexterity.Builtins;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;

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
                if (isAlive) 
                    return inst;
                
#if UNITY_EDITOR
                if (!EditorApplication.isPlaying)
                {
                    Debug.LogWarning("Trying to access Manager in edit mode, returning null. " +
                                     "If you want to check if Manager is alive, use Manager.isAlive instead.");
                    return null;
                }

                // This seems to catch the 'tear-down' state of the editor and avoid the long delay when quitting play-mode
                // (because every modifier/field tries to access Manager.instance and Unity decided it is now null) 
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    return null;
#endif
                    
                inst = FindFirstObjectByType<Manager>();
                if (inst == null)
                {
                    // manager is already dead
                    return null;
                }
                return inst;
            }
        }
        public static bool isAlive => inst != null;

        public DexteritySettings settings;
        private HashSet<Modifier> modifiers = new();
        private HashSet<BaseStateNode> nodes = new();
        private HashSet<UpdateableField> updateableFields = new();

        /// <summary>
        /// Adds a modifier to the update pool
        /// </summary>
        /// <param name="modifier">modifier to add</param>
        public void SubscribeToUpdates(Modifier modifier)
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
        public void UnsubscribeFromUpdates(Modifier modifier) => modifiers.Remove(modifier);

        /// <summary>
        /// Adds a node to the update pool
        /// </summary>
        /// <param name="node">node to add</param>
        public void SubscribeToUpdates(BaseStateNode node)
        {
            if (node == null)
            {
                Debug.LogError("Cannot add null node", node);
                return;
            }
            nodes.Add(node);
        }
        
        /// <summary>
        /// Removes a node from the update pool
        /// </summary>
        /// <param name="node">node to remove</param>
        public void UnsubscribeFromUpdates(BaseStateNode node) => nodes.Remove(node);
        
        /// <summary>
        /// Adds an updateable field to the update pool
        /// </summary>
        /// <param name="field">field to add</param>
        public void AddUpdateableField(UpdateableField field) => updateableFields.Add(field);

        /// <summary>
        /// Removes an updateable field from the update pool
        /// </summary>
        /// <param name="field">field to remove</param>
        public void RemoveUpdateableField(UpdateableField field) => updateableFields.Remove(field);

        protected void Awake()
        {
            if (Database.instance == null)
            {
                Database.Create(settings);
                settings.RuntimeInit();
            }
        }
        
        protected void OnDestroy()
        {
            Database.Destroy();
        }
        
        protected void Update()
        {
            
            // update all updateable fields
            if (updateableFields.Count > 0)
            {
                using (new ScopedProfile("Dexterity: Update Updateable Fields"))
                using (ListPool<UpdateableField>.Get(out var updateableFieldsActiveList))
                {
                    updateableFieldsActiveList.AddRange(updateableFields);
                    foreach (var field in updateableFieldsActiveList)
                    {
                        if (!field.pendingUpdate)
                            continue;

                        field.pendingUpdate = false;
                        try
                        {
                            field.Update();
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e, field.context);
                        }
                    }
                }
            }
            
            // update all nodes
            if (nodes.Count > 0)
            {
                using (new ScopedProfile("Dexterity: Update Nodes"))
                using (ListPool<BaseStateNode>.Get(out var nodesActiveList))
                {
                    nodesActiveList.AddRange(nodes);
                    foreach (var node in nodesActiveList)
                    {
                        try
                        {
                            node.Refresh();
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e, node);
                        }
                    }
                }
            }

            // update all modifiers
            if (modifiers.Count > 0)
            {
                using (new ScopedProfile("Dexterity: Update Modifiers"))
                using (ListPool<Modifier>.Get(out var modifiersActiveList))
                {
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
                                Debug.LogWarning(
                                    $"Modifier of type {modifier.GetType().Name} was destroyed but not removed, " +
                                    $"the exception above was probably caused by that reason. " +
                                    $"removing from update pool");
                                modifiers.Remove(modifier);
                            }
                        }
                    }
                }
            }
        }
    }
}
