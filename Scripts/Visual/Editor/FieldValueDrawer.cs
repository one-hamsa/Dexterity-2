using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    [CustomPropertyDrawer(typeof(FieldValueAttribute))]
    public class FieldValueDrawer : PropertyDrawer
    {
        
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {

            if (property.propertyType != SerializedPropertyType.Integer)
            {
                EditorGUI.LabelField(position, label.text, "Use [FieldValue] with ints.");
                return;
            }

            if (DexteritySettingsProvider.settings == null)
            {
                EditorGUI.LabelField(position, label.text, "Dexterity Settings not found.");
                return;
            }

            var attr = (FieldValueAttribute)attribute;
            string actualFieldName;
            if (attr.proxy)
            {
                var path = attr.fieldName;
                var dotPos = property.propertyPath.LastIndexOf('.');
                if (dotPos != -1)
                {
                    var parentPath = property.propertyPath.Substring(0, dotPos);
                    path = $"{parentPath}.{path}";
                }
                actualFieldName = property.serializedObject.FindProperty(path).stringValue;
            }
            else
            {
                actualFieldName = attr.fieldName;
            }

            if (string.IsNullOrWhiteSpace(actualFieldName))
            {
                property.intValue = EditorGUI.IntField(position, label.text, property.intValue);
                return;
            }

            var gateContainer = property.serializedObject.targetObject as IGateContainer;
            var definition = DexteritySettingsProvider.GetFieldDefinitionByName(gateContainer, actualFieldName);
            if (string.IsNullOrEmpty(definition.name)) {
                EditorGUI.LabelField(position, label.text, $"Field {actualFieldName} not found.");
                return;
            }

            switch (definition.type)
            {
                case Node.FieldType.Boolean:
                    property.intValue = EditorGUI.Popup(position, label.text, property.intValue, 
                        new string[] { "false", "true" });
                    break;
                case Node.FieldType.Enum:
                    property.intValue = EditorGUI.Popup(position, label.text, property.intValue,
                        definition.enumValues);
                    break;
            }
        }
    }
}