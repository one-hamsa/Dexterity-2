using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    [CustomPropertyDrawer(typeof(EnumFieldAttribute))]
    public class EnumFieldDrawer : PropertyDrawer
    {

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {

            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "Use [EnumField] with strings.");
                return;
            }
            
            var attr = attribute as EnumFieldAttribute;
            var path = attr.nodeFieldName;
            var dotPos = property.propertyPath.LastIndexOf('.');
            if (dotPos != -1)
            {
                var parentPath = property.propertyPath.Substring(0, dotPos);
                path = $"{parentPath}.{path}";
            }
            var unityObjectProp = property.serializedObject.FindProperty(path);

            if (unityObjectProp.objectReferenceValue == null)
                return;

            var enumNode = (EnumNode)unityObjectProp.objectReferenceValue;
            enumNode.InitializeObjectContext();
            
            if (!Enum.TryParse(enumNode.targetEnumType, property.stringValue, out var prevValue))
                // just take current value
                prevValue = enumNode.targetEnumValue;

            EditorGUI.BeginProperty(position, GUIContent.none, property);
            EditorGUI.BeginChangeCheck();
            var value = EditorGUI.EnumPopup(position, label.text, (Enum)prevValue);
            
            if (EditorGUI.EndChangeCheck() || string.IsNullOrEmpty(property.stringValue)) {
                property.stringValue = value.ToString();
            }
            EditorGUI.EndProperty();
        }
    }
}