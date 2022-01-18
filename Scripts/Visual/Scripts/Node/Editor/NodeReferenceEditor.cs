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
        bool foldoutOpen = true;
        public override void OnInspectorGUI()
        {
            reference = target as NodeReference;
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(NodeReference.stateFunctionAsset)));
            
            ShowExtends();
            var gatesUpdated = ShowGates(serializedObject.FindProperty(nameof(NodeReference.gates)),
                reference, ref foldoutOpen);
            ShowDelays();
            ShowWarnings();

            serializedObject.ApplyModifiedProperties();

            // do this after ApplyModifiedProperties() to ensure integrity
            if (gatesUpdated)
                reference.NotifyGatesUpdate();

            // always update if it's a live view
            if (reference.owner != null)
                this.Repaint();
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

            var functions = new HashSet<StateFunctionGraph>(new [] { reference.stateFunctionAsset }
                .Concat(reference.extends.Where(e => e != null).Select(e => e.stateFunctionAsset)));
            if (functions.Count > 1)
            {
                var names = string.Join(", ", functions.Select(f => f.name));
                EditorGUILayout.HelpBox($"Multiple state functions selected ({names})", MessageType.Error);
            }
        }

        private void ShowDelays()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(reference.delays)));
        }

        private void ShowExtends()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(reference.extends)));
        }


        internal static bool ShowGates(SerializedProperty gatesProp, IGateContainer gateContainer, ref bool foldoutOpen)
        {
            if (gateContainer.stateFunctionAsset == null)
                return false;

            if (!(foldoutOpen = EditorGUILayout.Foldout(foldoutOpen, $"Gates ({gateContainer.GetGateCount()})", EditorStyles.foldoutHeader)))
                return false;

            var updated = false;
            var gatesByField = new Dictionary<string, List<(int arrayIndex, SerializedProperty prop)>>();
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

                bool fieldExistsInFunction = gateContainer.stateFunctionAsset.GetFieldNames()
                    .ToList().IndexOf(kv.Key) != -1;

                if (string.IsNullOrEmpty(kv.Key) || !fieldExistsInFunction)
                    GUI.color = Color.yellow;
                else
                    GUI.color = Color.green;

                GUILayout.Label(!string.IsNullOrEmpty(kv.Key) ? kv.Key : "<unassigned>", EditorStyles.largeLabel);

                var definition = DexteritySettingsProvider.GetFieldDefinitionByName(kv.Key);
                if (!string.IsNullOrEmpty(kv.Key))
                {
                    if (definition.name != null)
                    {
                        // get value
                        var liveInstance = Application.isPlaying && gateContainer.node != null;
                        var value = liveInstance
                            ? gateContainer.node.GetOutputField(kv.Key).GetValue()
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
                    var gate = gateContainer.GetGateAtIndex(i);

                    // show output field dropdown
                    var outputProp = gateProp.FindPropertyRelative(nameof(Gate.outputFieldName));
                    var output = outputProp.stringValue;
                    // TODO check if manager exists!
                    var fields = gateContainer.stateFunctionAsset.GetFieldNames().ToArray();

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
                    EditorGUILayout.EndHorizontal();

                    if (definition.type == Node.FieldType.Boolean) {
                        GUI.contentColor = new Color(.7f, .7f, .7f);
                        EditorGUILayout.PropertyField(gateProp.FindPropertyRelative(nameof(Gate.overrideType)));

                        GUI.backgroundColor = origColor;
                        GUI.contentColor = origColor;

                        if (gate.overrideType.HasFlag(Gate.OverrideType.Subtractive)
                            && gate.overrideType.HasFlag(Gate.OverrideType.Additive) 
                            && indexInFieldType > 0) {
                            EditorGUILayout.HelpBox("This gate will override everything above it.", MessageType.Warning);
                        }
                    }
                    GUI.backgroundColor = origColor;
                    GUI.contentColor = origColor;

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
                gateContainer.AddGate(new Gate());
            }

            if (deleteIndex != -1)
            {
                Undo.RecordObject(gatesProp.serializedObject.targetObject, "Remove gate");
                gateContainer.RemoveGate(gateContainer.GetGateAtIndex(deleteIndex));
            }

            if (moveIndex != (-1, -1))
            {
                Undo.RecordObject(gatesProp.serializedObject.targetObject, "Move gates");

                // MoveArrayElement just makes unity crash, and since Gate is a Serializable 
                //. it looks like that's the most straightforward way around...
                var g1 = gateContainer.GetGateAtIndex(moveIndex.Item1);
                var g2 = gateContainer.GetGateAtIndex(moveIndex.Item2);

                var p1 = gatesProp.GetArrayElementAtIndex(moveIndex.Item1);
                var p2 = gatesProp.GetArrayElementAtIndex(moveIndex.Item2);

                p2.FindPropertyRelative(nameof(Gate.outputFieldName)).stringValue = g1.outputFieldName;
                p2.FindPropertyRelative(nameof(Gate.field)).managedReferenceValue = g1.field;

                p1.FindPropertyRelative(nameof(Gate.outputFieldName)).stringValue = g2.outputFieldName;
                p1.FindPropertyRelative(nameof(Gate.field)).managedReferenceValue = g2.field;

                updated = true;
            }
            updated |= updateIndex != -1;
            if (updated)
                EditorUtility.SetDirty(gatesProp.serializedObject.targetObject);

            return updated;
        }

        static bool ShowReference(string fieldName, SerializedProperty property)
        {
            bool updated = false;

            string className = Utils.GetClassName(property);
            var types = TypeCache.GetTypesDerivedFrom<BaseField>()
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