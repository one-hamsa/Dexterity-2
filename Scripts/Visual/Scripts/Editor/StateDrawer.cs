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

            var unityObject = property.serializedObject.targetObject;
            StateFunctionGraph sf;
            List<string> states = new List<string>();

            switch (unityObject)
            {
                case Modifier modifier:
                    sf = modifier.node?.referenceAsset?.stateFunctionAsset;
                    break;

                case Node node:
                    sf = node.referenceAsset?.stateFunctionAsset;
                    break;

                case NodeReference reference:
                    sf = reference.stateFunctionAsset;
                    break;
                default:
                    EditorGUI.LabelField(position, label.text, 
                        $"Unsupported object type {unityObject.GetType()} for attribute [State]");
                    return;
            }

            if (sf == null)
            {
                EditorGUI.LabelField(position, label.text,
                        $"State function not found for attribute [State]");
            }
            states.AddRange(sf.GetStates());

            var index = EditorGUI.Popup(position, label.text, states.IndexOf(property.stringValue), states.ToArray());
            if (index >= 0 && index < states.Count)
                property.stringValue = states[index];
        }
    }
}