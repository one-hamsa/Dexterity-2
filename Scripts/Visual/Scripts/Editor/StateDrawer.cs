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
            List<string> states = new List<string>();

            switch (unityObject)
            {
                case DexteritySettings settings:
                    foreach (var sf in settings.stateFunctions)
                        states.AddRange(sf.GetStates());
                    break;

                case Modifier modifier:
                    states.AddRange(modifier.node.referenceAsset.stateFunction.GetStates());
                    break;

                case Node node:
                    states.AddRange(node.referenceAsset.stateFunction.GetStates());
                    break;

                case NodeReference reference:
                    states.AddRange(reference.stateFunction.GetStates());
                    break;
                default:
                    EditorGUI.LabelField(position, label.text, 
                        $"Unsupported object type {unityObject.GetType()} for attribute [State]");
                    return;
            }
            
            var index = EditorGUI.Popup(position, label.text, states.IndexOf(property.stringValue), states.ToArray());
            if (index >= 0 && index < states.Count)
                property.stringValue = states[index];
        }
    }
}