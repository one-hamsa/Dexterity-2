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
        private static HashSet<BaseField> upstreams = new HashSet<BaseField>();

        private void OnEnable()
        {
            fieldValuesDebugOpen = Application.isPlaying;
            
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
            EditorGUILayout.HelpBox($"State functions are added automatically from references. You can change the order and add manual ones.", MessageType.Info);

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
                ShowSingleTargetDebug();
            ShowAllTargetsDebug();

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
            // add nice name for all overrides
            foreach (var o in node.overrides)
            {
                var definition = DexteritySettingsProvider.GetFieldDefinitionByName(o.outputFieldName);
                o.name = $"{definition.name} = {Utils.ConvertFieldValueToText(o.value, definition)}";
            }

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
                    node.SetStateOverride(Core.instance.GetStateID(overrideStateProp.stringValue));
            }
            GUI.enabled = true;
        }

        static bool modifiersDebugOpen;
        static bool stateFunctionsRuntimeDebugOpen;
        static bool fieldValuesDebugOpen;
        static bool upstreamDebugOpen;
        private static int speedIndex = -1;
        private EditorCoroutine coro;

        private int previewStateIndex;
        private HashSet<Node.OutputOverride> unusedOverrides = new HashSet<Node.OutputOverride>();
        private HashSet<Modifier> modifiers = new HashSet<Modifier>();

        void ShowSingleTargetDebug()
        {
            if (!Application.isPlaying)
            {
                ShowPreviewState();
            }
            else 
            {
                ShowActiveState();
                ShowRuntimeStateFunctions();
                ShowModifiers();
                ShowFieldValues();
            }
        }

        private void ShowModifiers()
        {
            var modifiers = Modifier.GetModifiers(node);

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

        private void ShowRuntimeStateFunctions()
        {
            if (node.reference != null && node.reference.stateFunctions.Length > 0)
            {
                if (!(stateFunctionsRuntimeDebugOpen = EditorGUILayout.Foldout(stateFunctionsRuntimeDebugOpen, "State Functions (Runtime)", true, EditorStyles.foldoutHeader)))
                    return;

                foreach (var function in node.reference.stateFunctions)
                {
                    // show state function button (play time)
                    if (GUILayout.Button(function.name))
                    {
                        EditorWindow.GetWindow<StateFunctionGraphWindow>().InitializeGraph(function);
                    }
                }
            }
        }

        private void ShowFieldValues()
        {
            if (!(fieldValuesDebugOpen = EditorGUILayout.Foldout(fieldValuesDebugOpen, "Field values", true, EditorStyles.foldoutHeader)))
                return;

            var origColor = GUI.color;

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
                string strValue = Utils.ConvertFieldValueToText(value, field.definition);

                if (value == Node.emptyFieldValue)
                {
                    GUI.color = Color.gray;
                    strValue = "(empty)";
                }
                if (overrides.ContainsKey(field.definitionId))
                {
                    var outputOverride = overrides[field.definitionId];
                    GUI.color = Color.magenta;
                    strValue = $"{Utils.ConvertFieldValueToText(outputOverride.value, field.definition)} ({StrikeThrough(strValue)})";
                    unusedOverrides.Remove(outputOverride);
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(Core.instance.GetFieldDefinition(field.definitionId).name);
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

        private void ShowActiveState()
        {
            var origColor = GUI.color;
            
            if (node.activeState != -1)
            {
                var style = new GUIStyle(EditorStyles.helpBox);
                style.alignment = TextAnchor.MiddleCenter;
                style.fontSize = 14;

                GUI.color = Color.green;
                GUILayout.Label(Core.instance.GetStateAsString(node.activeState), style);
                GUI.color = origColor;
            }
        }

        private void ShowAllTargetsDebug()
        {
            if (!Application.isPlaying)
                return;

            if (!(upstreamDebugOpen = EditorGUILayout.Foldout(upstreamDebugOpen, "Upstreams", true, EditorStyles.foldoutHeader)))
                return;

            foreach (var t in targets) {
                if (targets.Length > 1)
                    EditorGUILayout.LabelField(t.name, EditorStyles.whiteBoldLabel);

                foreach (var output in (t as Node).outputFields.Values)
                {
                    GUILayout.Label(output.definition.name, EditorStyles.boldLabel);

                    upstreams.Clear();
                    ShowUpstreams(output, t as Node);

                    GUILayout.Space(5);
                }

                GUILayout.Space(10);
            }
            
        }

        private static void ShowUpstreams(BaseField field, Node context)
        {
            upstreams.Add(field);

            if (Manager.instance.graph.edges.TryGetValue(field, out var upstreamFields)) {
                EditorGUI.indentLevel++;
                foreach (var upstreamField in upstreamFields) {
                    var upstreamFieldName = upstreamField.ToShortString();
                    var upstreamValue = upstreamField.GetValueAsString();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"{upstreamFieldName} = {upstreamValue}");
                    GUILayout.FlexibleSpace();
                    if (upstreamField.context != context && GUILayout.Button(upstreamField.context.name)) {
                        Selection.activeObject = upstreamField.context;
                    }
                    EditorGUILayout.EndHorizontal();

                    if (upstreams.Contains(upstreamField)) {
                        EditorGUILayout.HelpBox($"Cyclic dependency in {upstreamFieldName}", MessageType.Error);
                        continue;
                    }

                    ShowUpstreams(upstreamField, context);
                }
                EditorGUI.indentLevel--;
            }
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
