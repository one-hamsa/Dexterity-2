using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
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

            if (Manager.instance == null)
            {
                EditorGUI.LabelField(position, label.text, "Dexterity Manager not found.");
                return;
            }

            var sf = Utils.GetStateFunctionFromObject(property.serializedObject.targetObject);

            if (sf == null)
            {
                EditorGUI.LabelField(position, label.text,
                        $"State function not found for attribute [State]");
            }
            var states = sf.GetStates().ToList();
            var stateNames = states.ToList();
            var allowEmpty = (attribute as StateAttribute).allowEmpty;
            if (allowEmpty)
            {
                states.Insert(0, null);
                stateNames.Insert(0, "<None>");
            }

            var value = property.stringValue;
            if (string.IsNullOrEmpty(value))
                value = null;

            EditorGUI.BeginChangeCheck();
            var index = EditorGUI.Popup(position, label.text, states.IndexOf(value), stateNames.ToArray());
            if (EditorGUI.EndChangeCheck())
            {
                property.stringValue = states[index];
            }
        }
    }
}