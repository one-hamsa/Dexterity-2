using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OneHamsa.Dexterity
{
    public static class EditorTransitions
    {
        public static IEnumerator TransitionAsync(IEnumerable<Modifier> modifiers, 
            string fromState, string toState, float speed = 1f, Action onEnd = null)
        {
            if (Application.isPlaying)
            {
                Debug.LogError($"{nameof(TransitionAsync)} called in play mode");
                yield break;
            }
            
            modifiers = modifiers.ToList();
            // make sure it's not called with non-animatable modifiers
            if (modifiers.Any(m => !m.animatableInEditor))
            {
                Debug.LogError($"{nameof(TransitionAsync)} called with non-animatable modifiers");
                yield break;
            }

            // record all components on modifiers for undo
            foreach (var modifier in modifiers) {
                Undo.RegisterCompleteObjectUndo(modifier.GetComponents<Component>().ToArray(), "Editor Transition");
            }
            Undo.FlushUndoRecordObjects();
            
            // we don't know which components will be modified, so we have to dirty all of them
            void SetAllComponentsAndGameObjectsDirty()
            {
                foreach (var modifier in modifiers)
                {
                    foreach (var obj in modifier.GetComponents<Component>().Cast<Object>()
                                 .Concat(modifiers.Select(m => m.gameObject)))
                    {
                        EditorUtility.SetDirty(obj);
                    }
                }
            }

            try
            {
                // destroy previous instance, it's ok because it's editor time
                Database.Destroy();
                using var db = Database.Create(DexteritySettingsProvider.settings);

                foreach (var modifier in modifiers)
                    modifier.PrepareTransition_Editor(fromState, toState);

                SetAllComponentsAndGameObjectsDirty();
                bool anyChanged;
                do
                {
                    // imitate a frame
                    var beforeYield = EditorApplication.timeSinceStartup;
                    yield return null;
                    var dt = EditorApplication.timeSinceStartup - beforeYield;

                    // stop if something went wrong
                    if (Database.instance == null)
                        break;

                    // transition
                    anyChanged = false;
                    foreach (var modifier in modifiers)
                    {
                        // guard for async changes that might result in reference loss
                        if (modifier == null)
                            continue;
                        
                        // Core.instance.timeScale doesn't behave nicely in editor, so we have to do it manually
                        modifier.ProgressTime_Editor(dt * speed);

                        try
                        {
                            modifier.Refresh();
                            var changed = modifier.IsChanged();
                            anyChanged |= changed;
                            if (changed)
                                EditorUtility.SetDirty(modifier);
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e, modifier);
                        }
                    }

                    if (anyChanged)
                    {
                        SceneView.RepaintAll();
                    }
                } while (anyChanged);
            }
            finally
            {
                Database.Destroy();
                onEnd?.Invoke();
                SetAllComponentsAndGameObjectsDirty();
            }
        }
    }
}