using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System;

namespace OneHamsa.Dexterity.Visual
{
    [CustomEditor(typeof(Node))]
    public class NodeEditor : Editor
    {

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            ShowGates();
            ShowOverrides();
            ShowDebug();
            serializedObject.ApplyModifiedProperties();
        }
        void ShowGates()
        {
            var gatesProp = serializedObject.FindProperty(nameof(Node.gates));
            var gatesByField = new Dictionary<string, List<(int, SerializedProperty)>>();
            for (var i = 0; i < gatesProp.arraySize; ++i)
            {
                var gateProp = gatesProp.GetArrayElementAtIndex(i);
                var field = gateProp.FindPropertyRelative(nameof(Node.Gate.outputFieldName)).stringValue;
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
                GUI.color = !string.IsNullOrEmpty(kv.Key) ? Color.green : Color.yellow;
                GUILayout.Label(!string.IsNullOrEmpty(kv.Key) ? kv.Key : "<unassigned>", EditorStyles.largeLabel);
                if (!string.IsNullOrEmpty(kv.Key))
                {
                    GUILayout.FlexibleSpace();
                    GUI.color = Color.gray;
                    var style = new GUIStyle(EditorStyles.helpBox);
                    style.alignment = TextAnchor.MiddleLeft;
                    switch (Manager.Instance.GetFieldDefinition(kv.Key).Value.type)
                    {
                        case Node.FieldType.Boolean:
                            GUILayout.Label("Boolean", style);
                            break;
                        case Node.FieldType.Enum:
                            GUILayout.Label("Enum", style);
                            break;
                    }
                    GUI.color = Color.green;

                    if (GUILayout.Button("+", EditorStyles.miniButtonRight, GUILayout.Width(20)))
                    {
                        gatesProp.arraySize++;
                        // override new value
                        var newProp = gatesProp.GetArrayElementAtIndex(gatesProp.arraySize - 1);
                        newProp.FindPropertyRelative(nameof(Node.Gate.outputFieldName)).stringValue = kv.Key;
                        newProp.FindPropertyRelative(nameof(Node.Gate.field)).managedReferenceValue = null;
                    }
                }
                GUI.color = origColor;
                GUILayout.EndHorizontal();

                DrawSeparator(!string.IsNullOrEmpty(kv.Key) ? Color.green : Color.yellow);
                var indexInFieldType = 0;
                foreach ((var i, var gateProp) in kv.Value)
                {
                    // show output field dropdown
                    var outputProp = gateProp.FindPropertyRelative(nameof(Node.Gate.outputFieldName));
                    var output = outputProp.stringValue;
                    // TODO check if manager exists!
                    var fields = Manager.Instance.fieldDefinitions.Select(f => f.name).ToArray();

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
                    var fieldProp = gateProp.FindPropertyRelative(nameof(Node.Gate.field));
                    ShowReference(fieldProp);

                    DrawSeparator(Color.gray);

                    indexInFieldType++;
                }
            }

            if (GUILayout.Button("+", EditorStyles.miniButton))
            {
                gatesProp.arraySize++;
                // override new value
                var newProp = gatesProp.GetArrayElementAtIndex(gatesProp.arraySize - 1);
                newProp.FindPropertyRelative(nameof(Node.Gate.outputFieldName)).stringValue = null;
                newProp.FindPropertyRelative(nameof(Node.Gate.field)).managedReferenceValue = null;
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

                p2.FindPropertyRelative(nameof(Node.Gate.outputFieldName)).stringValue = g1.outputFieldName;
                p2.FindPropertyRelative(nameof(Node.Gate.field)).managedReferenceValue = g1.field;

                p1.FindPropertyRelative(nameof(Node.Gate.outputFieldName)).stringValue = g2.outputFieldName;
                p1.FindPropertyRelative(nameof(Node.Gate.field)).managedReferenceValue = g2.field;
            }
        }

        void ShowReference(SerializedProperty property)
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
                if (child.propertyType == SerializedPropertyType.ManagedReference)
                {
                    EditorGUILayout.LabelField(child.displayName, EditorStyles.boldLabel);
                    ShowReference(child);
                }
                else
                {
                    EditorGUILayout.PropertyField(child);
                }
            }
            EditorGUI.indentLevel--;
        }

        void ShowOverrides()
        {
            var overridesProp = serializedObject.FindProperty(nameof(Node.overrides));
            EditorGUILayout.PropertyField(overridesProp);
        }

        static bool debugOpen;
        void ShowDebug()
        {
            debugOpen = EditorGUILayout.Foldout(debugOpen, "Debug", true, EditorStyles.foldoutHeader);
            if (!debugOpen)
                return;

            var node = (Node)target;
            var outputFields = node.GetOutputFields();
            var overrides = node.GetOverrides();
            var unusedOverrides = new HashSet<Node.OutputOverride>(overrides.Values);
            var origColor = GUI.color;
            var overridesStr = overrides.Count == 0 ? "" : $", {overrides.Count} overrides";

            {                
                GUI.color = outputFields.Count == 0 ? Color.yellow : Color.grey;
                EditorGUILayout.LabelField($"{outputFields.Count} output fields{overridesStr}", EditorStyles.helpBox);
                GUI.color = origColor;
            }

            foreach (var field in outputFields.Values.OrderBy(f => f.GetValue() == Node.EMPTY_FIELD_VALUE))
            {
                var value = field.GetValueWithoutOverride();
                string strValue = value.ToString();
                if (value == Node.EMPTY_FIELD_VALUE)
                {
                    GUI.color = Color.gray;
                    strValue = "(empty)";
                }
                if (overrides.ContainsKey(field.name))
                {
                    var outputOverride = overrides[field.name];
                    GUI.color = Color.magenta;
                    strValue = $"{outputOverride.value} ({StrikeThrough(strValue)})";
                    unusedOverrides.Remove(outputOverride);
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(field.name);
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