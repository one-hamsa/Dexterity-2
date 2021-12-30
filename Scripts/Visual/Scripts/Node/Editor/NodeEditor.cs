using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System;
using Unity.EditorCoroutines.Editor;

namespace OneHamsa.Dexterity.Visual
{

    [CustomEditor(typeof(Node))]
    public class NodeEditor : Editor
    {
        Node node;
        bool foldoutOpen;

        private void OnEnable()
        {
            debugOpen = Application.isPlaying;
            
            foldoutOpen = (target as Node).customGates.Count > 0;
        }

        public override void OnInspectorGUI()
        {
            node = target as Node;

            serializedObject.Update();

            ShowChooseReference();
            ShowChooseFunction();
            ShowChooseInitialState();
            var gatesUpdated = NodeReferenceEditor.ShowGates(serializedObject.FindProperty(nameof(Node.customGates)),
                node, ref foldoutOpen);
            ShowOverrides();
            ShowDebug();
            ShowWarnings();
            serializedObject.ApplyModifiedProperties();

            // do this after ApplyModifiedProperties() to ensure integrity
            if (gatesUpdated)
                node.NotifyGatesUpdate();
        }

        private void ShowChooseInitialState()
        {
            if (node.referenceAssets.Count(a => a != null) == 0)
                return;

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

        private void ShowChooseFunction()
        {
            if (NodeReferenceEditor.ShowFunction(serializedObject.FindProperty(nameof(Node.stateFunctionAsset)), node))
                EditorUtility.SetDirty(node);

            if (node.stateFunctionAsset == null) 
            {
                var functions = new HashSet<StateFunctionGraph>(node.referenceAssets
                    .Where(a => a != null)
                    .Select(a => a.stateFunctionAsset));
                if (functions.Count == 1) {
                    node.stateFunctionAsset = functions.First();
                    EditorUtility.SetDirty(node);
                }
            }
        }


        void ShowOverrides()
        {
            var overridesProp = serializedObject.FindProperty(nameof(Node.overrides));
            EditorGUILayout.PropertyField(overridesProp);

            if (Application.isPlaying) {
                var overrideStateProp = serializedObject.FindProperty(nameof(Node.overrideState));
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(overrideStateProp);
                if (EditorGUI.EndChangeCheck())
                {
                    node.SetStateOverride(Manager.instance.GetStateID(overrideStateProp.stringValue));
                }
            }
        }

        static bool debugOpen;
        private int speedIndex = -1;
        private EditorCoroutine coro;

        void ShowDebug()
        {
            ShowPreviewState();

            if (!Application.isPlaying)
            {
                return;
            }
            var origColor = GUI.color;

            // show state function button (play time)
            if (GUILayout.Button("State Function Live View"))
            {
                EditorWindow.GetWindow<StateFunctionGraphWindow>().InitializeGraph(node.reference.stateFunction);
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
            var unusedOverrides = new HashSet<Node.OutputOverride>(overrides.Values);
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
            var states = node.stateFunctionAsset.GetStates().ToList();
            var stateNames = states.ToList();
            states.Insert(0, null);
            stateNames.Insert(0, "<None>");

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            GUILayout.Label("Preview");
            var index = EditorGUILayout.Popup("", 0, stateNames.ToArray());
            var didChange = EditorGUI.EndChangeCheck();

            var speeds = new [] { 0.1f, 0.25f, 0.5f, 1f, 1.5f, 2f };
            var speedsNames = speeds.Select(s => $"x{s}").ToArray();
            if (speedIndex == -1)
                speedIndex = Array.IndexOf(speeds, 1f);
            speedIndex = EditorGUILayout.Popup("", speedIndex, speedsNames, GUILayout.Width(50));

            if (didChange && states[index] != null)
            {
                if (coro != null)
                    EditorCoroutineUtility.StopCoroutine(coro);
                coro = EditorCoroutineUtility.StartCoroutine(
                    ModifierEditor.AnimateStateTransition(node, node.GetComponentsInChildren<Modifier>(), states[index]
                    , speeds[speedIndex]), this);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void ShowWarnings()
        {
            if (node.referenceAssets.Count(a => a != null) == 0)
            {
                EditorGUILayout.HelpBox("Must select Node Reference(s)", MessageType.Error);
            }
            if (node.stateFunctionAsset == null) 
            {
                EditorGUILayout.HelpBox($"No state functions selected", MessageType.Error);
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