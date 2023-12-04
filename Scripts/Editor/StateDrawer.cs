using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;

namespace OneHamsa.Dexterity
{
    [CustomPropertyDrawer(typeof(StateAttribute))]
    public class StateDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {

            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "Use [State] with strings.");
                return;
            }

            if (DexteritySettingsProvider.settings == null)
            {
                EditorGUI.LabelField(position, label.text, "Dexterity Settings not found.");
                return;
            }
            
            var attr = (StateAttribute)attribute;
            
            var obj = property.serializedObject.targetObject;
            if (!string.IsNullOrEmpty(attr.objectFieldName))
            {
                var path = attr.objectFieldName;
                var dotPos = property.propertyPath.LastIndexOf('.');
                if (dotPos != -1)
                {
                    var parentPath = property.propertyPath.Substring(0, dotPos);
                    path = $"{parentPath}.{path}";
                }
                
                obj = property.serializedObject.FindProperty(path).objectReferenceValue;
                if (obj == null)
                {
                    EditorGUI.LabelField(position, label.text,
                            $"Object not found for [State] attribute");
                    return;
                }
            }

            var statesSet = Utils.GetStatesFromObject(obj);
            if (statesSet == null)
            {
                EditorGUI.LabelField(position, label.text,
                        $"State function not found for attribute [State]");
                return;
            }

            var states = ListPool<string>.Get();
            var stateNames = ListPool<string>.Get();
            EditorGUI.BeginProperty(position, label, property);
            try
            {
                if (attr.allowEmpty)
                {
                    states.Add(null);
                    stateNames.Add("(None)");
                }

                foreach (var state in statesSet)
                {
                    states.Add(state);
                    stateNames.Add(state);
                }

                var value = property.stringValue;
                if (string.IsNullOrEmpty(value))
                    value = null;

                EditorGUI.BeginChangeCheck();
                var index = EditorGUI.Popup(position, label.text, states.IndexOf(value), stateNames.ToArray());
                if (EditorGUI.EndChangeCheck())
                {
                    property.stringValue = states[index];
                    EditorUtility.SetDirty(property.serializedObject.targetObject);
                }
            }
            finally
            {
                ListPool<string>.Release(states);
                ListPool<string>.Release(stateNames);
                EditorGUI.EndProperty();
            }
        }
    }
}