using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
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

            if (Manager.instance == null)
            {
                EditorGUI.LabelField(position, label.text, "Dexterity Manager not found.");
                return;
            }

            var attr = attribute as FieldValueAttribute;
            string actualFieldName;
            if (attr.proxy)
            {
                var parentPath = property.propertyPath.Substring(0, property.propertyPath.LastIndexOf('.'));
                var parent = property.serializedObject.FindProperty(parentPath);
                actualFieldName = parent.FindPropertyRelative(attr.fieldName).stringValue;
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

            var definition = Manager.instance.GetFieldDefinitionByName(actualFieldName);

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