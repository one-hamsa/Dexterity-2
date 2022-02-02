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
        private List<string> states = new List<string>();
        private List<string> stateNames = new List<string>();

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

            var statesSet = Utils.GetStatesFromObject(property.serializedObject.targetObject);

            if (statesSet == null)
            {
                EditorGUI.LabelField(position, label.text,
                        $"State function not found for attribute [State]");
                return;
            }

            states.Clear();
            stateNames.Clear();

            var allowEmpty = (attribute as StateAttribute).allowEmpty;
            if (allowEmpty)
            {
                states.Add(null);
                stateNames.Add("(None)");
            }

            foreach (var state in statesSet) {
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
    }
}