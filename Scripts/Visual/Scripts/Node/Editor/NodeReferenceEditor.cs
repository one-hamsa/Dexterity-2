using UnityEngine;
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
            ShowGates();
            serializedObject.ApplyModifiedProperties();
        }

        private void ShowFunction()
        {
            var functions = DexteritySettingsProvider.settings.stateFunctions
                .Where(f => f != null).Select(f => f.name).ToArray();

            var stateFunctionProperty = serializedObject.FindProperty(nameof(Modifier.stateFunction));
            var stateFunctionObj = (StateFunctionGraph)stateFunctionProperty.objectReferenceValue;

            EditorGUI.BeginChangeCheck();
            var stateFunctionIdx = EditorGUILayout.Popup("State Function",
                Array.IndexOf(functions, stateFunctionObj?.name), functions);

            if (EditorGUI.EndChangeCheck() && stateFunctionIdx >= 0)
            {
                var stateFunction = DexteritySettingsProvider.GetStateFunctionByName(functions[stateFunctionIdx]);
                stateFunctionProperty.objectReferenceValue = stateFunction;
            }

            if (reference.stateFunction != null && GUILayout.Button("Open State Function Graph"))
            {
                EditorWindow.GetWindow<StateFunctionGraphWindow>().InitializeGraph(reference.stateFunction);
            }
        }

        void ShowGates()
        {
            if (reference.stateFunction == null)
                return;

            var gatesProp = serializedObject.FindProperty(nameof(NodeReference.gates));
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
            foreach (var kv in gatesByField)
            {
                GUILayout.BeginHorizontal();
                var origColor = GUI.color;

                bool fieldExistsInFunction = reference.stateFunction.GetFieldNames()
                    .ToList().IndexOf(kv.Key) != -1;

                if (string.IsNullOrEmpty(kv.Key) || !fieldExistsInFunction)
                    GUI.color = Color.yellow;
                else
                    GUI.color = Color.green;

                GUILayout.Label(!string.IsNullOrEmpty(kv.Key) ? kv.Key : "<unassigned>", EditorStyles.largeLabel);

                if (!string.IsNullOrEmpty(kv.Key))
                {
                    GUILayout.FlexibleSpace();
                    GUI.color = Color.gray;
                    var style = new GUIStyle(EditorStyles.helpBox);
                    style.alignment = TextAnchor.MiddleLeft;

                    var definition = DexteritySettingsProvider.GetFieldDefinitionByName(kv.Key);
                    if (definition.name != null)
                    {
                        switch (definition.type)
                        {
                            case Node.FieldType.Boolean:
                                GUILayout.Label("Boolean", style);
                                break;
                            case Node.FieldType.Enum:
                                GUILayout.Label("Enum", style);
                                break;
                        }
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
                    var fields = reference.stateFunction.GetFieldNames().ToArray();

                    EditorGUILayout.BeginHorizontal();
                    if (!string.IsNullOrEmpty(kv.Key))
                    {
                        GUI.backgroundColor = Color.clear;
                        GUI.contentColor = Color.gray;
                    }
                    var outputIdx = EditorGUILayout.Popup($"Output Field {indexInFieldType + 1}",
                        Array.IndexOf(fields, output), fields);
                    if (outputIdx >= 0)
                        outputProp.stringValue = fields[outputIdx];

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
                    ShowReference(fieldName, fieldProp);

                    DrawSeparator(Color.gray);

                    indexInFieldType++;
                }
            }

            if (GUILayout.Button("+", EditorStyles.miniButton))
            {
                gatesProp.arraySize++;
                // override new value
                var newProp = gatesProp.GetArrayElementAtIndex(gatesProp.arraySize - 1);
                newProp.FindPropertyRelative(nameof(Gate.outputFieldName)).stringValue = null;
                newProp.FindPropertyRelative(nameof(Gate.field)).managedReferenceValue = null;
            }

            if (deleteIndex != -1)
                gatesProp.DeleteArrayElementAtIndex(deleteIndex);

            if (moveIndex != (-1, -1))
            {
                // MoveArrayElement just makes unity crash, and since Gate is a Serializable 
                //. it looks like that's the most straightforward way around...
                var g1 = ((Node)target).gates[moveIndex.Item1];
                var g2 = ((Node)target).gates[moveIndex.Item2];

                var p1 = gatesProp.GetArrayElementAtIndex(moveIndex.Item1);
                var p2 = gatesProp.GetArrayElementAtIndex(moveIndex.Item2);

                p2.FindPropertyRelative(nameof(Gate.outputFieldName)).stringValue = g1.outputFieldName;
                p2.FindPropertyRelative(nameof(Gate.field)).managedReferenceValue = g1.field;

                p1.FindPropertyRelative(nameof(Gate.outputFieldName)).stringValue = g2.outputFieldName;
                p1.FindPropertyRelative(nameof(Gate.field)).managedReferenceValue = g2.field;
            }
        }

        void ShowReference(string fieldName, SerializedProperty property)
        {
            string className = Utils.GetClassName(property);
            var types = Utils.GetSubtypes<BaseField>()
                .Where(t => (bool)t.GetField(nameof(BaseField.showInInspector), 
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy).GetValue(null))
                .ToArray();
            var fieldTypesNames = types
                .Select(t => t.ToString())
                .ToArray();
            var currentIdx = Array.IndexOf(fieldTypesNames, className);
            var fieldIdx = EditorGUILayout.Popup("Field", currentIdx,
                Utils.GetNiceName(fieldTypesNames, suffix: "Field").ToArray());

            if (fieldIdx >= 0 && currentIdx != fieldIdx)
            {
                var type = types[fieldIdx];
                property.managedReferenceValue = Activator.CreateInstance(type);
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
                    ShowReference(fieldName, child);
                }
                else
                {
                    EditorGUILayout.PropertyField(child);
                }
            }
            EditorGUI.indentLevel--;
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