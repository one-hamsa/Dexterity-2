using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System;
using Unity.EditorCoroutines.Editor;
using UnityEditor.SceneManagement;
using UnityEngine.UIElements;

namespace OneHamsa.Dexterity.Visual
{
    public abstract class DexterityBaseNodeEditor : Editor
    {
        static bool modifiersDebugOpen;
        private static int speedIndex = -1;
        
        DexterityBaseNode baseNode;
        private HashSet<string> states = new HashSet<string>();
        private List<string> previewStates = new List<string>();
        private List<string> previewStateNames = new List<string>();
        private EditorCoroutine coro;

        private int previewStateIndex;
        private HashSet<Modifier> modifiers = new HashSet<Modifier>();

        protected virtual void Legacy_OnInspectorGUI()
        {
            states.Clear();
            var first = true;
            foreach (var t in targets) {
                foreach (var state in (t as DexterityBaseNode).GetStateNames()) {
                    if (states.Add(state) && !first) {
                        EditorGUILayout.HelpBox("Can't multi-edit nodes with different state lists.", MessageType.Error);
                        return;
                    }
                }
                first = false;
            }

            baseNode = target as DexterityBaseNode;

            serializedObject.Update();

            EditorGUILayout.LabelField("Fields & State", EditorStyles.whiteLargeLabel);

            ShowChooseInitialState();

            ShowFields();

            ShowDelays();

            ShowOverrides();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Debug", EditorStyles.whiteLargeLabel);
            
            if (targets.Length <= 1)
                ShowSingleTargetDebug();
            ShowAllTargetsDebug();

            ShowWarnings();
            serializedObject.ApplyModifiedProperties();
        }

        protected abstract void ShowFields();

        private void ShowChooseInitialState()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(DexterityBaseNode.initialState)));
        }

        void ShowOverrides()
        {
            ShowFieldOverrides();
            ShowStateOverride();
        }

        protected virtual void ShowFieldOverrides()
        {
            
        }

        private void ShowStateOverride()
        {
            if (targets.Length > 1)
                return;

            GUI.enabled = Application.isPlaying;
            var overrideStateProp = serializedObject.FindProperty(nameof(DexterityBaseNode.overrideState));

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(overrideStateProp, new GUIContent("State Override"));
            if (EditorGUI.EndChangeCheck())
            {
                if (string.IsNullOrEmpty(overrideStateProp.stringValue))
                    baseNode.ClearStateOverride();
                else
                    baseNode.SetStateOverride(Core.instance.GetStateID(overrideStateProp.stringValue));
            }
            GUI.enabled = true;
        }

        void ShowSingleTargetDebug()
        {
            if (!Application.isPlaying)
            {
                ShowPreviewState();
            }
            else 
            {
                ShowActiveState();
                ShowModifiers();
                ShowFieldValues();
            }
        }

        private void ShowModifiers()
        {
            var modifiers = Modifier.GetModifiers(baseNode);

            if (!(modifiersDebugOpen = EditorGUILayout.Foldout(modifiersDebugOpen, $"Modifiers ({modifiers.Count()})", true, EditorStyles.foldoutHeader)))
                return;

            foreach (var m in modifiers)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{m.name}: {m.GetType().Name}");
                if (GUILayout.Button("Go"))
                {
                    Selection.activeObject = m;
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void ShowDelays()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(DexterityBaseNode.delays)));
        }

        protected virtual void ShowFieldValues()
        {
        }

        private void ShowActiveState()
        {
            var origColor = GUI.color;
            
            if (baseNode.activeState != -1)
            {
                var style = new GUIStyle(EditorStyles.helpBox);
                style.alignment = TextAnchor.MiddleCenter;
                style.fontSize = 14;

                GUI.color = Color.green;
                GUILayout.Label(Core.instance.GetStateAsString(baseNode.activeState), style);
                GUI.color = origColor;
            }
        }

        protected virtual void ShowAllTargetsDebug()
        {
        }

        private void ShowPreviewState()
        {
            previewStates.Clear();
            previewStateNames.Clear();

            previewStates.Add(null);
            previewStateNames.Add("(None)");

            foreach (var state in states) {
                previewStates.Add(state);
                previewStateNames.Add(state);
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            var propDrawer = new StateDrawer();
            GUILayout.Label("Preview");
            var newIndex = EditorGUILayout.Popup("", previewStateIndex, previewStateNames.ToArray());
            if (newIndex != 0)
                previewStateIndex = newIndex;

            var didChange = EditorGUI.EndChangeCheck();

            var origColor = GUI.contentColor;
            GUI.contentColor = coro != null ? Color.green : origColor;

            var speeds = new [] { 0.1f, 0.25f, 0.5f, 1f, 1.25f, 1.5f, 2f };
            var speedsNames = speeds.Select(s => $"x{s}").ToArray();
            if (speedIndex == -1)
                speedIndex = Array.IndexOf(speeds, 1f);
            speedIndex = EditorGUILayout.Popup("", speedIndex, speedsNames, GUILayout.Width(50));

            GUI.contentColor = origColor;

            if (didChange && previewStates[previewStateIndex] != null)
            {
                if (coro != null)
                    EditorCoroutineUtility.StopCoroutine(coro);

                // collect all children modifiers
                modifiers.Clear();
                // see https://forum.unity.com/threads/findobjectsoftype-is-broken-when-invoked-from-inside-prefabstage-nested-prefabs.684037/
                foreach (var modifier in Resources.FindObjectsOfTypeAll<Modifier>()) {
                    if (modifier.node == baseNode && modifier.isActiveAndEnabled)
                    {
                        if (!modifier.animatableInEditor)
                        {
                            Debug.LogWarning($"{modifier.GetType().Name} is not animatable in editor. It will not be previewed.", modifier);
                            continue;
                        }
                        modifiers.Add(modifier);
                    }
                }

                coro = EditorCoroutineUtility.StartCoroutine(
                    ModifierEditor.AnimateStateTransition(baseNode, modifiers, previewStates[previewStateIndex]
                    , speeds[speedIndex], () => coro = null), this);
            }
            EditorGUILayout.EndHorizontal();
        }

        protected virtual void ShowWarnings()
        {
            if (!states.Contains(baseNode.initialState))
            {
                EditorGUILayout.HelpBox($"Initial State should be selected", MessageType.Warning);
            }
            if (targets.Length > 1) 
            {
                EditorGUILayout.HelpBox($"Some options are hidden in multi-edit mode", MessageType.Warning);
            }
        }

        public static string StrikeThrough(string s)
        {
            string strikethrough = "";
            foreach (char c in s)
            {
                strikethrough = strikethrough + c + '\u0336';
            }
            return strikethrough;
        }

        protected static void DrawSeparator(Color color)
        {
            EditorGUILayout.Space();
            var rect = EditorGUILayout.BeginHorizontal();
            Handles.color = color;
            Handles.DrawLine(new Vector2(rect.x, rect.y), new Vector2(rect.width + 15, rect.y));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }
    }
}
