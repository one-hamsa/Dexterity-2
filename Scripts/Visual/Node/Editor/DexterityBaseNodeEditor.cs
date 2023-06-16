using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System;
using System.Collections;
using Unity.EditorCoroutines.Editor;
using UnityEditor.SceneManagement;
using UnityEngine.UIElements;

namespace OneHamsa.Dexterity
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
        private HashSet<Modifier> modifiers;
        private bool modifiersCacheInvalidated => modifiers == null || lastModifiersUpdateTime < EditorApplication.timeSinceStartup - 1f;
        private double lastModifiersUpdateTime;

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
            var origColor = GUI.contentColor;
            GUI.contentColor = Color.yellow;
            EditorGUILayout.LabelField("Debug", EditorStyles.whiteLargeLabel);
            GUI.contentColor = origColor;
            
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
                    baseNode.SetStateOverride(Database.instance.GetStateID(overrideStateProp.stringValue));
            }
            GUI.enabled = true;
        }

        void ShowSingleTargetDebug()
        {
            if (!Application.isPlaying)
            {
                ShowPreviewState();
            }
            
            ShowModifiers();
                
            if (Application.isPlaying)
            {
                ShowActiveState();
                ShowFieldValues();
            }
        }

        private void ShowModifiers()
        {
            var modifiers = GetModifiers().ToList();
            
            // make sure all are up to date
            foreach (var modifier in modifiers)
                ModifierEditor.SyncModifierStates(modifier);
            
            if (!(modifiersDebugOpen = EditorGUILayout.Foldout(modifiersDebugOpen, $"Modifiers ({modifiers.Count()})", true, EditorStyles.foldoutHeader)))
                return;
            
            if (GUILayout.Button("Save Current As..."))
            {
                // show menu to select which state to save
                var menu = new GenericMenu();
                foreach (var state in states)
                {
                    menu.AddItem(new GUIContent(state), false, () => SaveModifiers(state));
                }
                menu.ShowAsContext();
                
                void SaveModifiers(string state)
                {
                    foreach (var modifier in modifiers)
                    {
                        if (modifier is ISupportPropertyFreeze freeze)
                        {
                            var activeProp = modifier.properties.First(p => p.state == state);
                            freeze.FreezeProperty(activeProp);
                        }
                        else 
                        {
                            Debug.LogWarning($"Modifier {modifier.name} does not support property freeze, skipping.", modifier);
                        }
                    }
                }
            }

            var origColor = GUI.contentColor;
            foreach (var m in modifiers)
            {
                EditorGUILayout.BeginHorizontal();
                var icon = EditorGUIUtility.ObjectContent(m, m.GetType());
                EditorGUILayout.LabelField(icon);
                if (GUILayout.Button(EditorGUIUtility.IconContent("d_Prefab Icon"), GUILayout.MaxWidth(30), GUILayout.MaxHeight(20)))
                {
                    Selection.activeObject = m;
                }
                EditorGUILayout.EndHorizontal();
                GUI.contentColor = new Color(.75f, .75f, .75f);
                EditorGUILayout.LabelField(GetPath(m.gameObject), EditorStyles.miniLabel);
                GUI.contentColor = origColor;
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
            
            if (baseNode.GetActiveState() != -1)
            {
                var style = new GUIStyle(EditorStyles.helpBox);
                style.alignment = TextAnchor.MiddleCenter;
                style.fontSize = 14;

                GUI.color = Color.green;
                GUILayout.Label(Database.instance.GetStateAsString(baseNode.GetActiveState()), style);
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

            var origColor = GUI.contentColor;
            var origBgColor = GUI.backgroundColor;
            GUI.contentColor = new Color(1f, 1f, .75f);
            GUI.backgroundColor = new Color(1f, 1f, .75f);

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            GUILayout.Label("Preview");
            var newIndex = EditorGUILayout.Popup("", previewStateIndex, previewStateNames.ToArray(),
                GUILayout.MaxWidth(150));
            if (newIndex != 0)
                previewStateIndex = newIndex;

            var didChange = EditorGUI.EndChangeCheck();

            GUI.backgroundColor = origBgColor;
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
                var allModifiers = GetModifiers();
                var modifiers = new HashSet<Modifier>();
                foreach (var modifier in allModifiers)
                {
                    if (!modifier.animatableInEditor)
                    {
                        Debug.LogWarning($"{modifier.GetType().Name} is not animatable in editor. It will not be previewed.", modifier);
                        continue;
                    }
                    modifiers.Add(modifier);   
                }

                coro = EditorCoroutineUtility.StartCoroutine(
                    ModifierEditor.AnimateStateTransition(modifiers, previewStates[previewStateIndex]
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
        
        private IEnumerable<Modifier> GetModifiers()
        {
            if (Application.isPlaying)
                return Modifier.GetModifiers(baseNode);

            if (!modifiersCacheInvalidated)
                return modifiers;
            
            modifiers = new HashSet<Modifier>();
            
            // see https://forum.unity.com/threads/findobjectsoftype-is-broken-when-invoked-from-inside-prefabstage-nested-prefabs.684037/
            foreach (var modifier in Resources.FindObjectsOfTypeAll<Modifier>()) {
                if (modifier.GetNode() == baseNode && modifier.isActiveAndEnabled) 
                    modifiers.Add(modifier);
            }

            lastModifiersUpdateTime = EditorApplication.timeSinceStartup;
            return modifiers;
        }
        
        private static string GetPath(GameObject go)
        {
            string name = go.name;
            while (go.transform.parent != null)
            {

                go = go.transform.parent.gameObject;
                name = go.name + "/" + name;
            }
            return name;
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
