﻿using UnityEngine;
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

    [CustomEditor(typeof(Node)), CanEditMultipleObjects]
    public class NodeEditor : DexterityBaseNodeEditor
    {
        static bool fieldValuesDebugOpen;
        static bool upstreamDebugOpen;
        private static HashSet<BaseField> upstreams = new HashSet<BaseField>();
        Node node;
        bool foldoutOpen;
        
        private HashSet<Node.OutputOverride> unusedOverrides = new HashSet<Node.OutputOverride>();
        private bool gatesUpdated;

        protected void OnEnable()
        {
            foldoutOpen = true;
            fieldValuesDebugOpen = Application.isPlaying;
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            root.Add(new IMGUIContainer(Legacy_OnInspectorGUI_ChooseReference));

            var foldout = new Foldout { text = "Evaluation Steps" };
            foldout.style.unityFontStyleAndWeight = FontStyle.Bold;
            foldout.style.marginLeft = 10;
            foldout.contentContainer.style.unityFontStyleAndWeight = FontStyle.Normal;
            foldout.Add(new StepListView(serializedObject, nameof(Node.customSteps)));
            root.Add(foldout);
            
            // TODO 
            // EditorGUILayout.HelpBox($"State functions are added automatically from references. You can change the order and add manual ones.", MessageType.Info);
            
            root.Add(new IMGUIContainer(Legacy_OnInspectorGUI));

            return root;
        }

        private void Legacy_OnInspectorGUI_ChooseReference() {
            node = target as Node;

            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(Node.referenceAssets)));

            // runtime
            if (node.reference != null) {
                if (GUILayout.Button("Open Live Reference"))
                {
                    NodeReferenceEditorWindow.Open(node.reference); 
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        protected override void Legacy_OnInspectorGUI()
        {
            gatesUpdated = false;
            base.Legacy_OnInspectorGUI();

            // do this after ApplyModifiedProperties() to ensure integrity
            if (gatesUpdated)
                node.NotifyGatesUpdate();
        }

        private void ShowChooseInitialState()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(Node.initialState)));
        }

        protected override void ShowFieldOverrides()
        {
            // add nice name for all overrides
            foreach (var o in node.overrides)
            {
                var definition = DexteritySettingsProvider.GetFieldDefinitionByName(o.outputFieldName);
                o.name = $"{definition.name} = {Utils.ConvertFieldValueToText(o.value, definition)}";
            }

            var overridesProp = serializedObject.FindProperty(nameof(Node.overrides));
            EditorGUILayout.PropertyField(overridesProp, new GUIContent("Field Overrides"));
        }

        protected override void ShowFields()
        {
            if (targets.Length <= 1)
                gatesUpdated = NodeReferenceEditor.ShowGates(serializedObject.FindProperty(nameof(Node.customGates)),
                    node, ref foldoutOpen);
        }


        private void ShowDelays()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(Node.delays)));
        }

        protected override void ShowFieldValues()
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

        protected override void ShowAllTargetsDebug()
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

        protected override void ShowWarnings()
        {
            if (node.customSteps.Count == 0)
            {
                EditorGUILayout.HelpBox($"Node has no steps", MessageType.Error);
            }
            base.ShowWarnings();
        }
    }
}
