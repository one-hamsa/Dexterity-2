using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.UIElements;
using System;
using UnityEditor.UIElements;
using UnityEngine.Pool;

namespace OneHamsa.Dexterity
{

    [CustomEditor(typeof(FieldNode)), CanEditMultipleObjects]
    public class FieldNodeEditor : BaseStateNodeEditor
    {
        static bool fieldValuesDebugOpen;
        FieldNode node;
        
        private HashSet<FieldNode.OutputOverride> unusedOverrides = new();
        private bool gatesUpdated;
        private StepListView stepListView;

        protected void OnEnable()
        {
            fieldValuesDebugOpen = Application.IsPlaying(target);
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            root.Add(new IMGUIContainer(Legacy_OnInspectorGUI_ChooseReference));

            var foldout = new Foldout { text = "Evaluation Steps" };
            foldout.style.unityFontStyleAndWeight = FontStyle.Bold;
            foldout.style.marginLeft = 10;
            foldout.contentContainer.style.unityFontStyleAndWeight = FontStyle.Normal;
            
            stepListView = new StepListView(serializedObject, nameof(FieldNode.customSteps));
            foreach (var node in targets.OfType<FieldNode>())
            {
                node.onStateChanged += OnNodeStateChanged;
            }
            
            foldout.Add(stepListView);
            
            // disallow editing in play mode - this would require re-initialization of StepList
            foldout.SetEnabled(!Application.IsPlaying(target));
            root.Add(foldout);

            // TODO 
            // EditorGUILayout.HelpBox($"State functions are added automatically from references. You can change the order and add manual ones.", MessageType.Info);
            
            root.Add(new IMGUIContainer(Legacy_OnInspectorGUI));

            return root;
        }

        private void OnNodeStateChanged(int oldState, int newState)
        {
            try 
            {
                stepListView.RefreshItems();
            }
            catch (Exception e) 
            {
                Debug.LogException(e, target);
            }
        }

        private void OnDestroy()
        {
            foreach (var node in targets.OfType<FieldNode>())
            {
                node.onStateChanged -= OnNodeStateChanged;
            }
        }

        private void Legacy_OnInspectorGUI_ChooseReference() {
            node = target as FieldNode;

            serializedObject.Update();
            
            // XXX call this from here because adding to customSteps from OnValidate() literally causes editor to crash
            //. when selecting multiple editor targets
            foreach (var node in targets.Cast<FieldNode>())
                node.FixSteps();

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(FieldNode.referenceAssets)));

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
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(FieldNode.initialState)));
        }

        protected override void ShowFieldOverrides()
        {
            // add nice name for all overrides
            foreach (var o in node.overrides)
            {
                if (string.IsNullOrEmpty(o.outputFieldName))
                    continue;
                
                var definition = DexteritySettingsProvider.GetFieldDefinitionByName(node, o.outputFieldName);
                if (string.IsNullOrEmpty(definition.GetName()))
                    definition.SetName_Editor($"(unknown: {o.outputFieldName})");
                    
                o.name = $"{definition.GetName()} = {Utils.ConvertFieldValueToText(o.value, definition)}";
            }

            var overridesProp = serializedObject.FindProperty(nameof(FieldNode.overrides));
            EditorGUILayout.PropertyField(overridesProp, new GUIContent("Field Overrides"));
        }

        protected override void ShowFields()
        {
            if (targets.Length <= 1)
                gatesUpdated = NodeReferenceEditor.ShowGates(serializedObject.FindProperty(nameof(FieldNode.customGates)),
                    node);
            
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(FieldNode.internalFieldDefinitions)));
        }

        protected override void ShowFieldValues()
        {
            if (!(fieldValuesDebugOpen = EditorGUILayout.Foldout(fieldValuesDebugOpen, "Field values", true, EditorStyles.foldoutHeader)))
                return;

            var origColor = GUI.color;

            var outputFields = node.outputFields;
            var overrides = node.cachedOverrides;
            unusedOverrides.Clear();
            foreach (var pair in overrides.keyValuePairs)
                unusedOverrides.Add(pair.Value);

            var overridesStr = overrides.Count == 0 ? "" : $", {overrides.Count} overrides";
            {
                EditorGUILayout.HelpBox($"{outputFields.Count} output fields{overridesStr}",
                    outputFields.Count == 0 ? MessageType.Warning : MessageType.Info);
            }

            foreach (var pair in outputFields.keyValuePairs.OrderBy(f => f.Value.value == BaseField.emptyFieldValue))
            {
                var field = pair.Value;
                var value = field.GetValueWithoutOverride();
                string strValue = Utils.ConvertFieldValueToText(value, field.definition);

                if (value == BaseField.emptyFieldValue)
                {
                    GUI.color = Color.gray;
                    strValue = "(empty)";
                }
                if (overrides.TryGetValue(field.definitionId, out var valueOverride))
                {
                    GUI.color = Color.magenta;
                    strValue = $"{Utils.ConvertFieldValueToText(valueOverride.value, field.definition)} ({StrikeThrough(strValue)})";
                    unusedOverrides.Remove(valueOverride);
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(field.definition.GetName());
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
