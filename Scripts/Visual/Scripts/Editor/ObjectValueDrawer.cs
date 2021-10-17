using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    [CustomPropertyDrawer(typeof(ObjectValueAttribute))]
    public class ObjectValueDrawer : PropertyDrawer
    {
        
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "Use [ObjectValue] with strings.");
                return;
            }

            var attr = attribute as ObjectValueAttribute;
            var parentPath = property.propertyPath.Substring(0, property.propertyPath.LastIndexOf('.'));
            var parent = property.serializedObject.FindProperty(parentPath);
            var unityObjectProp = parent.FindPropertyRelative(attr.objectFieldName);

            if (unityObjectProp.objectReferenceValue == null)
                return;

            var obj = unityObjectProp.objectReferenceValue;
            var objType = obj.GetType();

            var options = new List<MemberInfo>();
            foreach (var method in objType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.GetParameters().Length == 0 && method.ReturnType == attr.fieldType)
                    options.Add(method);
            }
            foreach (var field in objType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType == attr.fieldType)
                    options.Add(field);
            }
            foreach (var prop in objType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.PropertyType == attr.fieldType)
                    options.Add(prop);
            }
            var stringOptions = options.Select(o => o.Name).ToList();

            EditorGUI.BeginChangeCheck();
            var index = EditorGUI.Popup(position, label.text, stringOptions.IndexOf(property.stringValue),
                        options.Select(o => $"{o.DeclaringType}::{o.Name}").ToArray());
            if (EditorGUI.EndChangeCheck())
                property.stringValue = stringOptions[index];
        }
    }
}