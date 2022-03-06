using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System;
using Unity.EditorCoroutines.Editor;
using UnityEditor.SceneManagement;

namespace OneHamsa.Dexterity.Visual
{

    [CustomEditor(typeof(Node)), CanEditMultipleObjects]
    public class NodeEditor : Editor
    {
        Node node;
        bool foldoutOpen;
        private HashSet<string> sfStates = new HashSet<string>();
        private List<string> previewStates = new List<string>();
        private List<string> previewStateNames = new List<string>();

        private void OnEnable()
        {
            debugOpen = Application.isPlaying;
            
            foldoutOpen = true;
        }

        public override void OnInspectorGUI()
        {
            sfStates.Clear();
            var first = true;
            foreach (var t in targets) {
                foreach (var state in (t as IStatesProvider).GetStateNames()) {
                    if (sfStates.Add(state) && !first) {
                        EditorGUILayout.HelpBox("Can't multi-edit nodes with different state lists.", MessageType.Error);
                        return;
                    }
                }
                first = false;
            }

            node = target as Node;

            serializedObject.Update();

            ShowChooseReference();
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(Node.stateFunctionAssets)));

            if (targets.Length <= 1) {
                EditorGUILayout.HelpBox($"State Functions exection order: \n"
                + string.Join(" -> ", node.GetStateFunctionAssetsIncludingReferences().Select(r => r.name)), MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Fields & State", EditorStyles.whiteLargeLabel);

            ShowChooseInitialState();

            var gatesUpdated = false;
            if (targets.Length <= 1)
                gatesUpdated = NodeReferenceEditor.ShowGates(serializedObject.FindProperty(nameof(Node.customGates)),
                    node, ref foldoutOpen);

            ShowOverrides();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Debug", EditorStyles.whiteLargeLabel);
            
            if (targets.Length <= 1)
                ShowDebug();

            ShowWarnings();
            serializedObject.ApplyModifiedProperties();

            // do this after ApplyModifiedProperties() to ensure integrity
            if (gatesUpdated)
                node.NotifyGatesUpdate();
        }

        private void ShowChooseInitialState()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(Node.initialState)));
        }

        void ShowChooseReference()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(Node.referenceAssets)));

            // runtime
            if (node.reference != null) {
                if (GUILayout.Button("Open Live Reference"))
                {
                    NodeReferenceEditorWindow.Open(node.reference); 
                }
            }
        }

        void ShowOverrides()
        {
            var overridesProp = serializedObject.FindProperty(nameof(Node.overrides));
            EditorGUILayout.PropertyField(overridesProp, new GUIContent("Field Overrides"));

            if (targets.Length > 1)
                return;

            GUI.enabled = Application.isPlaying;
            var overrideStateProp = serializedObject.FindProperty(nameof(Node.overrideState));

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(overrideStateProp, new GUIContent("State Override"));
            if (EditorGUI.EndChangeCheck())
            {
                if (string.IsNullOrEmpty(overrideStateProp.stringValue))
                    node.ClearStateOverride();
                else
                    node.SetStateOverride(Manager.instance.GetStateID(overrideStateProp.stringValue));
            }
            GUI.enabled = true;
        }

        static bool debugOpen;
        private static int speedIndex = -1;
        private EditorCoroutine coro;

        private int previewStateIndex;
        private HashSet<Node.OutputOverride> unusedOverrides = new HashSet<Node.OutputOverride>();
        private HashSet<Modifier> modifiers = new HashSet<Modifier>();

        void ShowDebug()
        {
            if (!Application.isPlaying)
            {
                ShowPreviewState();
                return;
            }
            var origColor = GUI.color;

            if (node.reference != null && node.reference.stateFunctions.Length > 0) {
                EditorGUILayout.LabelField("State Functions (Runtime)", EditorStyles.whiteLargeLabel);
                foreach (var function in node.reference.stateFunctions) {
                    // show state function button (play time)
                    if (GUILayout.Button(function.name))
                    {
                        EditorWindow.GetWindow<StateFunctionGraphWindow>().InitializeGraph(function);
                    }
                }
            }

            if (node.activeState != -1)
            {
                var style = new GUIStyle(EditorStyles.helpBox);
                style.alignment = TextAnchor.MiddleCenter;
                style.fontSize = 14;

                GUI.color = Color.green;
                GUILayout.Label(Manager.instance.GetStateAsString(node.activeState), style);
                GUI.color = origColor;
            }

            debugOpen = EditorGUILayout.Foldout(debugOpen, "Debug", true, EditorStyles.foldoutHeader);
            if (!debugOpen)
                return;

            var outputFields = node.outputFields;
            var overrides = node.cachedOverrides;
            unusedOverrides.Clear();
            foreach (var value in overrides.Values)
                unusedOverrides.Add(value);

            var overridesStr = overrides.Count == 0 ? "" : $", {overrides.Count} overrides";
            {                
                EditorGUILayout.HelpBox($"{outputFields.Count} output fields{overridesStr}",
                    outputFields.Count == 0 ? MessageType.Warning : MessageType.Info);
            }

            foreach (var field in outputFields.Values.ToArray().OrderBy(f => f.GetValue() == Node.emptyFieldValue))
            {
                var value = field.GetValueWithoutOverride();
                string strValue = value.ToString();
                if (value == Node.emptyFieldValue)
                {
                    GUI.color = Color.gray;
                    strValue = "(empty)";
                }
                if (overrides.ContainsKey(field.definitionId))
                {
                    var outputOverride = overrides[field.definitionId];
                    GUI.color = Color.magenta;
                    strValue = $"{outputOverride.value} ({StrikeThrough(strValue)})";
                    unusedOverrides.Remove(outputOverride);
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(Manager.instance.GetFieldDefinition(field.definitionId).name);
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(strValue);
                EditorGUILayout.EndHorizontal();

                GUI.color = origColor;
            }

            foreach (var outputOverride in unusedOverrides)
            {
                GUI.color = Color.magenta;
                    
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(outputOverride.outputFieldName);
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(outputOverride.value.ToString());
                EditorGUILayout.EndHorizontal();

                GUI.color = origColor;
            }

            Repaint();
        }

        private void ShowPreviewState()
        {
            previewStates.Clear();
            previewStateNames.Clear();

            previewStates.Add(null);
            previewStateNames.Add("(None)");

            foreach (var state in sfStates) {
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

            var speeds = new [] { 0.1f, 0.25f, 0.5f, 1f, 1.5f, 2f };
            var speedsNames = speeds.Select(s => $"x{s}").ToArray();
            if (speedIndex == -1)
                speedIndex = Array.IndexOf(speeds, 1f);
            speedIndex = EditorGUILayout.Popup("", speedIndex, speedsNames, GUILayout.Width(50));

            if (didChange && previewStates[previewStateIndex] != null)
            {
                if (coro != null)
                    EditorCoroutineUtility.StopCoroutine(coro);

                // collect all children modifiers
                modifiers.Clear();
                // see https://forum.unity.com/threads/findobjectsoftype-is-broken-when-invoked-from-inside-prefabstage-nested-prefabs.684037/
                foreach (var modifier in StageUtility.GetCurrentStageHandle().FindComponentsOfType<Modifier>()) {
                    if (modifier.node == node)
                        modifiers.Add(modifier);
                }

                coro = EditorCoroutineUtility.StartCoroutine(
                    ModifierEditor.AnimateStateTransition(node, modifiers, previewStates[previewStateIndex]
                    , speeds[speedIndex]), this);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void ShowWarnings()
        {
            if (node.stateFunctionAssets.Count == 0 && node.referenceAssets.Count == 0)
            {
                EditorGUILayout.HelpBox($"No state functions or references assigned to node", MessageType.Error);
            }
            if (!sfStates.Contains(node.initialState))
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

        static void DrawSeparator(Color color)
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
