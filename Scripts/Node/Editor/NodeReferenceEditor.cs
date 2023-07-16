using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System;
using System.Globalization;

namespace OneHamsa.Dexterity
{
    using Gate = NodeReference.Gate;

    [CustomEditor(typeof(NodeReference))]
    public class NodeReferenceEditor : Editor
    {
        NodeReference reference;
        bool foldoutOpen = true;
        private static Dictionary<string, List<(int arrayIndex, SerializedProperty prop)>> gatesByField = new();

        public override void OnInspectorGUI()
        {
            reference = target as NodeReference;
            serializedObject.Update();

            ShowExtends();

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(NodeReference.stateFunctionAssets)));
            EditorGUILayout.HelpBox($"State functions are added automatically from references. You can change the order and add manual ones.", MessageType.Info);

            var gatesUpdated = ShowGates(serializedObject.FindProperty(nameof(NodeReference.gates)),
                reference, ref foldoutOpen);
            
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(FieldNode.internalFieldDefinitions)));

            serializedObject.ApplyModifiedProperties();

            // do this after ApplyModifiedProperties() to ensure integrity
            if (gatesUpdated)
                reference.NotifyGatesUpdate();

            // always update if it's a live view
            if (reference.owner != null)
                this.Repaint();
        }

        private void ShowExtends()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(reference.extends)));
        }


        internal static bool ShowGates(SerializedProperty gatesProp, IGateContainer gateContainer, ref bool foldoutOpen)
        {
            var rect = EditorGUILayout.BeginVertical();
            EditorGUI.BeginProperty(rect, GUIContent.none, gatesProp);

            foldoutOpen = EditorGUILayout.Foldout(foldoutOpen, $"Gates ({gateContainer.GetGateCount()})", EditorStyles.foldoutHeader);

            EditorGUI.EndProperty();
            EditorGUILayout.EndVertical();

            if (!foldoutOpen) {
                return false;
            }

            var updated = false;
            gatesByField.Clear();
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

                var whitelist = gateContainer.GetWhitelistedFieldNames();
                var fields = DexteritySettingsProvider.settings.fieldDefinitions.Select(fd => fd.name)
                    .Where(f => whitelist == null || whitelist.Contains(f))
                    .Concat(gateContainer.GetInternalFieldDefinitions().Select(fd => fd.GetInternalName()))
                    .ToArray();
                
                var fieldExistsInFunction = fields.Contains(kv.Key);
                var fieldIsInternal = FieldDefinition.IsInternalName(kv.Key);

                if (string.IsNullOrEmpty(kv.Key) || !fieldExistsInFunction)
                    GUI.color = Color.yellow;
                else
                    GUI.color = !fieldIsInternal ? Color.green : Color.cyan;

                GUILayout.Label(!string.IsNullOrEmpty(kv.Key) ? kv.Key : "<unassigned>", EditorStyles.largeLabel);

                var definition = ExtractDefinition(gateContainer, kv.Key);
                if (!string.IsNullOrEmpty(kv.Key))
                {
                    if (definition.name != null)
                    {
                        // get value
                        var liveInstance = Application.isPlaying && gateContainer.node != null;
                        var value = liveInstance
                            ? gateContainer.node.GetOutputField(kv.Key).GetValue()
                            : FieldNode.defaultFieldValue;

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

                    rect = EditorGUILayout.BeginHorizontal();
                    EditorGUI.BeginProperty(rect, GUIContent.none, outputProp);
                    
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
                    EditorGUI.EndProperty();
                    EditorGUILayout.EndHorizontal();

                    if (definition.type == FieldNode.FieldType.Boolean) {
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

        private static FieldDefinition ExtractDefinition(IGateContainer gateContainer, string fieldName)
        {
            return !string.IsNullOrEmpty(fieldName)
                ? DexteritySettingsProvider.GetFieldDefinitionByName(gateContainer, fieldName)
                : default;
        }

        static double lastReferenceRefreshTime = double.NegativeInfinity;
        private static Type[] referencesTypes;
        private static string[] referencesTypesNames;

        static bool ShowReference(string fieldName, SerializedProperty property)
        {
            bool updated = false;

            var rect = EditorGUILayout.BeginVertical();
            EditorGUI.BeginProperty(rect, GUIContent.none, property);

            string className = Utils.GetClassName(property);
            RefreshReferenceTypes();
            var currentIdx = Array.IndexOf(referencesTypesNames, className);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            var fieldIdx = EditorGUILayout.Popup("Field", currentIdx,
                Utils.GetNiceName(referencesTypesNames, suffix: "Field").ToArray());

            var field = (BaseField)Utils.GetTargetObjectOfProperty(property);
            if (field != null && field.initialized)
            {
                DrawFieldValue(field.definition, field.GetValue(), true);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.EndProperty();
            EditorGUILayout.EndVertical();

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(property.serializedObject.targetObject, "Change field type");
                var type = referencesTypes[fieldIdx];
                property.managedReferenceValue = Activator.CreateInstance(type);
                updated = true;
            }

            EditorGUI.indentLevel++;
            foreach (var child in Utils.GetChildren(property))
            {
                if (child.name == nameof(BaseField.relatedFieldName))
                {
                    if (child.stringValue != fieldName)
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
                    
                    // XXX unity has some weird bug here when it's a bool (NodeField's negate for instance)
                    //. toggling with the keyboard (space) works (?!), but clicking doesn't - feels like
                    //. something is taking control over this UI but I couldn't find what. so here's another Unity HACK
                    if (!updated 
                        && child.propertyType == SerializedPropertyType.Boolean 
                        && Event.current?.type == EventType.MouseDown)
                    {
                        var lastRect = GUILayoutUtility.GetLastRect();
                        if (Event.current.button == 0 && lastRect.Contains(Event.current.mousePosition))
                        {
                            child.boolValue = !child.boolValue;
                            updated = true;
                            Event.current.Use();
                        }
                    }
                }
            }
            EditorGUI.indentLevel--;

            return updated;
        }

        private static void RefreshReferenceTypes()
        {
            if (EditorApplication.timeSinceStartup - lastReferenceRefreshTime < 3)
                return;

            referencesTypes = TypeCache.GetTypesDerivedFrom<BaseField>()
                            .Where(t => t != typeof(FieldNode.OutputField))
                            .ToArray();
            referencesTypesNames = referencesTypes
                .Select(t => t.ToString())
                .ToArray();
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
                    case FieldNode.FieldType.Boolean when value == 0:
                    case FieldNode.FieldType.Boolean when value == FieldNode.defaultFieldValue:
                        GUI.color = Color.red;
                        valueName = "false";
                        break;
                    case FieldNode.FieldType.Boolean when value == 1:
                        GUI.color = Color.green;
                        valueName = "true";
                        break;
                    case FieldNode.FieldType.Enum:
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
                    case FieldNode.FieldType.Boolean:
                        valueName = "Boolean";
                        break;
                    case FieldNode.FieldType.Enum:
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
            var origColor = Handles.color;
            Handles.color = color;
            
            var rect = EditorGUILayout.BeginHorizontal();
            Handles.DrawLine(new Vector2(rect.x, rect.y), new Vector2(rect.width + 15, rect.y));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
            
            Handles.color = origColor;
        }
    }
}