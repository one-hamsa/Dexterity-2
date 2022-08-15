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
            var path = attr.objectFieldName;
            var dotPos = property.propertyPath.LastIndexOf('.');
            if (dotPos != -1)
            {
                var parentPath = property.propertyPath.Substring(0, dotPos);
                path = $"{parentPath}.{path}";
            }
            var unityObjectProp = property.serializedObject.FindProperty(path);

            if (unityObjectProp.objectReferenceValue == null)
                return;

            var obj = unityObjectProp.objectReferenceValue;
            var objType = obj.GetType();

            var options = new List<MemberInfo>();
            foreach (var method in objType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.GetParameters().Length == 0 && attr.fieldType.IsAssignableFrom(method.ReturnType))
                    options.Add(method);
            }
            foreach (var field in objType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (attr.fieldType.IsAssignableFrom(field.FieldType))
                    options.Add(field);
            }
            foreach (var prop in objType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (attr.fieldType.IsAssignableFrom(prop.PropertyType))
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