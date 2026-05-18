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
        /// <summary>
        /// Drive multiple modifier groups through their (fromState→toState) transitions
        /// in parallel under a single shared <see cref="Database"/> session. Avoids the
        /// race that one-at-a-time serialization was guarding against (each
        /// <see cref="TransitionAsync"/> call does <c>Database.Destroy()</c> +
        /// <c>Database.Create()</c>; running two concurrently would tear down each
        /// other's instance). Use this when you have several independent transitions
        /// to animate simultaneously (e.g. a parent node + dependent nodes).
        /// </summary>
        public static IEnumerator TransitionMultipleAsync(
            IEnumerable<(IEnumerable<Modifier> modifiers, string fromState, string toState)> groups,
            float speed = 1f, Action onEnd = null)
        {
            if (Application.isPlaying)
            {
                Debug.LogError($"{nameof(TransitionMultipleAsync)} called in play mode");
                yield break;
            }

            // Flatten groups; remember per-modifier from/to so we prepare each correctly.
            var pairs = new List<(Modifier m, string from, string to)>();
            foreach (var (mods, from, to) in groups)
                foreach (var m in mods)
                    if (m != null) pairs.Add((m, from, to));

            if (pairs.Count == 0) { onEnd?.Invoke(); yield break; }
            if (pairs.Any(p => !p.m.animatableInEditor))
            {
                Debug.LogError($"{nameof(TransitionMultipleAsync)} called with non-animatable modifiers");
                yield break;
            }

            // Record undo for every involved component.
            foreach (var (m, _, _) in pairs)
                Undo.RegisterCompleteObjectUndo(m.GetComponents<Component>().ToArray(), "Editor Transition");
            Undo.FlushUndoRecordObjects();

            void SetAllDirty()
            {
                foreach (var (m, _, _) in pairs)
                {
                    foreach (var obj in m.GetComponents<Component>().Cast<Object>())
                        EditorUtility.SetDirty(obj);
                    EditorUtility.SetDirty(m.gameObject);
                }
            }

            try
            {
                // ONE Database session covering all parallel transitions.
                Database.Destroy();
                using var db = Database.Create(DexteritySettingsProvider.settings);

                foreach (var (m, from, to) in pairs)
                    m.PrepareTransition_Editor(from, to);

                SetAllDirty();
                bool anyChanged;
                do
                {
                    var beforeYield = EditorApplication.timeSinceStartup;
                    yield return null;
                    var dt = EditorApplication.timeSinceStartup - beforeYield;
                    if (Database.instance == null) break;

                    anyChanged = false;
                    foreach (var (m, _, _) in pairs)
                    {
                        if (m == null) continue;
                        m.ProgressTime_Editor(dt * speed);
                        try
                        {
                            m.Refresh();
                            var changed = m.IsChanged();
                            anyChanged |= changed;
                            if (changed) EditorUtility.SetDirty(m);
                        }
                        catch (Exception e) { Debug.LogException(e, m); }
                    }
                    if (anyChanged) SceneView.RepaintAll();
                } while (anyChanged);
            }
            finally
            {
                Database.Destroy();
                onEnd?.Invoke();
                SetAllDirty();
            }
        }

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