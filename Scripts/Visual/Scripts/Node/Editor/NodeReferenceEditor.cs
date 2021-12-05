﻿using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System;

namespace OneHamsa.Dexterity.Visual
{
    using Gate = NodeReference.Gate;

    [CustomEditor(typeof(NodeReference))]
    public class NodeReferenceEditor : Editor
    {
        NodeReference reference;
        public override void OnInspectorGUI()
        {
            reference = target as NodeReference;
            serializedObject.Update();

            ShowFunction();
            var gatesUpdated = ShowGates(serializedObject.FindProperty(nameof(NodeReference.gates)),
                reference);
            ShowDelays();
            ShowDefaultStrategy();
            ShowWarnings();

            serializedObject.ApplyModifiedProperties();

            // do this after ApplyModifiedProperties() to ensure integrity
            if (gatesUpdated)
                reference.NotifyGatesUpdate();
        }

        private void ShowWarnings()
        {
            var sf = reference.stateFunctionAsset;
            if (sf == null)
            {
                EditorGUILayout.HelpBox($"No state function selected", MessageType.Error);
            }
            else if (!sf.Validate())
            {
                EditorGUILayout.HelpBox($"{sf.name}: {sf.errorString}", MessageType.Error);
            }
        }

        private void ShowDefaultStrategy()
        {
            var prop = serializedObject.FindProperty(nameof(reference.defaultStrategy));

            var types = Utils.GetSubtypes<ITransitionStrategy>();
            var typesNames = types
                .Select(t => t.ToString())
                .ToArray();

            EditorGUI.BeginChangeCheck();
            var currentIdx = Array.IndexOf(typesNames, prop.stringValue);
            var fieldIdx = EditorGUILayout.Popup("Default Transition Strategy", currentIdx,
                Utils.GetNiceName(typesNames, suffix: "Strategy").ToArray());
            if (EditorGUI.EndChangeCheck())
            {
                prop.stringValue = typesNames[fieldIdx];
            }
        }

        private void ShowDelays()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(reference.delays)));
        }

        private void ShowFunction()
        {
            var functions = Utils.FindAssetsByType<StateFunctionGraph>().ToList();
            var funcNames = functions.Select(f => f.name).ToArray();

            var stateFunctionProperty = serializedObject.FindProperty(nameof(reference.stateFunctionAsset));
            var stateFunctionObj = (StateFunctionGraph)stateFunctionProperty.objectReferenceValue;

            EditorGUI.BeginChangeCheck();
            var stateFunctionIdx = EditorGUILayout.Popup("State Function",
                Array.IndexOf(funcNames, stateFunctionObj?.name), funcNames);

            if (EditorGUI.EndChangeCheck() && stateFunctionIdx >= 0)
            {
                var stateFunction = functions[stateFunctionIdx];
                stateFunctionProperty.objectReferenceValue = stateFunction;
            }

            if (reference.stateFunctionAsset != null && GUILayout.Button("Open State Function Graph"))
            {
                EditorWindow.GetWindow<StateFunctionGraphWindow>().InitializeGraph(reference.stateFunctionAsset);
            }
        }

        internal static bool ShowGates(SerializedProperty gatesProp, IFieldHolder fieldHolder)
        {
            if (fieldHolder.fieldsStateFunction == null)
                return false;


            var updated = false;
            var gatesByField = new Dictionary<string, List<(int, SerializedProperty)>>();
            for (var i = 0; i < gatesProp.arraySize; ++i)
            {
                var gateProp = gatesProp.GetArrayElementAtIndex(i);
                var field = gateProp.FindPropertyRelative(nameof(Gate.outputFieldName)).stringValue;
                List<(int, SerializedProperty)> lst;
                if (!gatesByField.TryGetValue(field, out lst))
                {
                    lst = gatesByField[field] = new List<(int, SerializedProperty)>();
                }
                lst.Add((i, gateProp));
            }

            var deleteIndex = -1;
            var moveIndex = (-1, -1);
            var updateIndex = -1;
            foreach (var kv in gatesByField)
            {
                GUILayout.BeginHorizontal();
                var origColor = GUI.color;

                bool fieldExistsInFunction = fieldHolder.fieldsStateFunction.GetFieldNames()
                    .ToList().IndexOf(kv.Key) != -1;

                if (string.IsNullOrEmpty(kv.Key) || !fieldExistsInFunction)
                    GUI.color = Color.yellow;
                else
                    GUI.color = Color.green;

                GUILayout.Label(!string.IsNullOrEmpty(kv.Key) ? kv.Key : "<unassigned>", EditorStyles.largeLabel);

                if (!string.IsNullOrEmpty(kv.Key))
                {
                    var definition = DexteritySettingsProvider.GetFieldDefinitionByName(kv.Key);

                    if (definition.name != null)
                    {
                        // get value
                        var liveInstance = Application.isPlaying && fieldHolder.node != null;
                        var value = liveInstance
                            ? fieldHolder.node.GetOutputField(kv.Key).GetValue()
                            : Node.defaultFieldValue;

                        GUILayout.FlexibleSpace();
                        DrawFieldValue(definition, value, liveInstance);
                    }
                    GUI.color = Color.green;

                    if (GUILayout.Button("+", EditorStyles.miniButtonRight, GUILayout.Width(20)))
                    {
                        gatesProp.arraySize++;
                        // override new value
                        var newProp = gatesProp.GetArrayElementAtIndex(gatesProp.arraySize - 1);
                        newProp.FindPropertyRelative(nameof(Gate.outputFieldName)).stringValue = kv.Key;
                        newProp.FindPropertyRelative(nameof(Gate.field)).managedReferenceValue = null;
                    }
                }
                GUI.color = origColor;
                GUILayout.EndHorizontal();

                DrawSeparator(!string.IsNullOrEmpty(kv.Key) ? Color.green : Color.yellow);

                if (!fieldExistsInFunction)
                {
                    GUI.color = Color.yellow;
                    EditorGUILayout.LabelField($"Add field to state function", EditorStyles.helpBox);
                    GUI.color = origColor;
                }

                var indexInFieldType = 0;
                foreach ((var i, var gateProp) in kv.Value)
                {
                    // show output field dropdown
                    var outputProp = gateProp.FindPropertyRelative(nameof(Gate.outputFieldName));
                    var output = outputProp.stringValue;
                    // TODO check if manager exists!
                    var fields = fieldHolder.fieldsStateFunction.GetFieldNames().ToArray();

                    EditorGUILayout.BeginHorizontal();
                    if (!string.IsNullOrEmpty(kv.Key))
                    {
                        GUI.backgroundColor = Color.clear;
                        GUI.contentColor = Color.gray;
                    }
                    EditorGUI.BeginChangeCheck();
                    var outputIdx = EditorGUILayout.Popup($"Output Field {indexInFieldType + 1}",
                        Array.IndexOf(fields, output), fields);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(gatesProp.serializedObject.targetObject, "Change output field");
                        outputProp.stringValue = fields[outputIdx];
                        updateIndex = i;
                    }

                    GUI.contentColor = indexInFieldType > 0 ? Color.white : Color.gray;
                    EditorGUI.BeginDisabledGroup(indexInFieldType == 0);
                    if (GUILayout.Button('\u25B2'.ToString(), EditorStyles.miniButtonRight, GUILayout.Width(20)))
                    {
                        moveIndex = (i, kv.Value[indexInFieldType - 1].Item1);
                    }
                    EditorGUI.EndDisabledGroup();

                    GUI.contentColor = indexInFieldType < kv.Value.Count - 1 ? Color.white : Color.gray;
                    EditorGUI.BeginDisabledGroup(indexInFieldType == kv.Value.Count - 1);
                    if (GUILayout.Button('\u25BC'.ToString(), EditorStyles.miniButtonRight, GUILayout.Width(20)))
                    {
                        moveIndex = (i, kv.Value[indexInFieldType + 1].Item1);
                    }
                    EditorGUI.EndDisabledGroup();

                    GUI.contentColor = Color.red;
                    if (GUILayout.Button("-", EditorStyles.miniButtonRight, GUILayout.Width(20)))
                    {
                        deleteIndex = i;
                    }
                    GUI.backgroundColor = origColor;
                    GUI.contentColor = origColor;
                    EditorGUILayout.EndHorizontal();

                    // show field (create new reference if doesnt exist)
                    var fieldProp = gateProp.FindPropertyRelative(nameof(Gate.field));
                    var fieldName = gateProp.FindPropertyRelative(nameof(Gate.outputFieldName)).stringValue;
                    if (ShowReference(fieldName, fieldProp))
                        updateIndex = i;

                    DrawSeparator(Color.gray);

                    indexInFieldType++;
                }
            }

            if (GUILayout.Button("+", EditorStyles.miniButton))
            {
                Undo.RecordObject(gatesProp.serializedObject.targetObject, "Add gate");
                fieldHolder.AddGate(new Gate());
            }

            if (deleteIndex != -1)
            {
                Undo.RecordObject(gatesProp.serializedObject.targetObject, "Remove gate");
                fieldHolder.RemoveGate(fieldHolder.GetGateAtIndex(deleteIndex));
            }

            if (moveIndex != (-1, -1))
            {
                Undo.RecordObject(gatesProp.serializedObject.targetObject, "Move gates");

                // MoveArrayElement just makes unity crash, and since Gate is a Serializable 
                //. it looks like that's the most straightforward way around...
                var g1 = fieldHolder.GetGateAtIndex(moveIndex.Item1);
                var g2 = fieldHolder.GetGateAtIndex(moveIndex.Item2);

                var p1 = gatesProp.GetArrayElementAtIndex(moveIndex.Item1);
                var p2 = gatesProp.GetArrayElementAtIndex(moveIndex.Item2);

                p2.FindPropertyRelative(nameof(Gate.outputFieldName)).stringValue = g1.outputFieldName;
                p2.FindPropertyRelative(nameof(Gate.field)).managedReferenceValue = g1.field;

                p1.FindPropertyRelative(nameof(Gate.outputFieldName)).stringValue = g2.outputFieldName;
                p1.FindPropertyRelative(nameof(Gate.field)).managedReferenceValue = g2.field;

                updated = true;
            }
            updated |= updateIndex != -1;

            return updated;
        }

        static bool ShowReference(string fieldName, SerializedProperty property)
        {
            bool updated = false;

            string className = Utils.GetClassName(property);
            var types = Utils.GetSubtypes<BaseField>()
                .Where(t => (bool)t.GetField(nameof(BaseField.showInInspector), 
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy).GetValue(null))
                .ToArray();
            var fieldTypesNames = types
                .Select(t => t.ToString())
                .ToArray();
            var currentIdx = Array.IndexOf(fieldTypesNames, className);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            var fieldIdx = EditorGUILayout.Popup("Field", currentIdx,
                Utils.GetNiceName(fieldTypesNames, suffix: "Field").ToArray());

            var field = (BaseField)Utils.GetTargetObjectOfProperty(property);
            if (field != null && field.initialized)
            {
                DrawFieldValue(field.definition, field.GetValue(), true);
            }
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(property.serializedObject.targetObject, "Change field type");
                var type = types[fieldIdx];
                property.managedReferenceValue = Activator.CreateInstance(type);
                updated = true;
            }

            EditorGUI.indentLevel++;
            foreach (var child in Utils.GetChildren(property))
            {
                if (child.name == nameof(BaseField.relatedFieldName))
                {
                    child.stringValue = fieldName;
                }
                else if (child.propertyType == SerializedPropertyType.ManagedReference)
                {
                    EditorGUILayout.LabelField(child.displayName, EditorStyles.boldLabel);
                    updated |= ShowReference(fieldName, child);
                }
                else
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(child);
                    updated |= EditorGUI.EndChangeCheck();
                }
            }
            EditorGUI.indentLevel--;

            return updated;
        }

        static void DrawFieldValue(FieldDefinition definition, int value, bool liveInstance)
        {
            // get value
            string valueName = "";
            var origColor = GUI.color;

            if (liveInstance)
            {
                switch (definition.type)
                {
                    case Node.FieldType.Boolean when value == 0:
                    case Node.FieldType.Boolean when value == Node.defaultFieldValue:
                        GUI.color = Color.red;
                        valueName = "false";
                        break;
                    case Node.FieldType.Boolean when value == 1:
                        GUI.color = Color.green;
                        valueName = "true";
                        break;
                    case Node.FieldType.Enum:
                        GUI.color = new Color(1, .5f, 0);
                        valueName = definition.enumValues[value];
                        break;
                }
            }
            else
            {
                GUI.color = Color.gray;
                switch (definition.type)
                {
                    case Node.FieldType.Boolean:
                        valueName = "Boolean";
                        break;
                    case Node.FieldType.Enum:
                        valueName = "Enum";
                        break;
                }
            }

            var style = new GUIStyle(EditorStyles.helpBox);
            style.alignment = TextAnchor.MiddleCenter;

            GUILayout.Label(valueName, style, GUILayout.Width(60));
            GUI.color = origColor;
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