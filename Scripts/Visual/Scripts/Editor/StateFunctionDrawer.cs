using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    //[CustomPropertyDrawer(typeof(StateFunction))]
    public class StateFunctionDrawer : PropertyDrawer
    {
        private List<StateFunction> functions = new List<StateFunction>();
        private double lastRefresh = double.NegativeInfinity;
        private string[] funcNames;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return base.GetPropertyHeight(property, label) + EditorGUIUtility.singleLineHeight;
        }
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {

            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                EditorGUI.LabelField(position, label.text, "Use [StateFunction] with StateFunction.");
                return;
            }

            RefreshStateFunctionList();
            var stateFunctionObj = (StateFunction)property.objectReferenceValue;

            var r = position;
            r.height = EditorGUIUtility.singleLineHeight;

            EditorGUI.BeginChangeCheck();
            EditorGUI.BeginProperty(position, GUIContent.none, property);
            var stateFunctionIdx = EditorGUI.Popup(r, "State Function",
                Array.IndexOf(funcNames, stateFunctionObj?.name), funcNames);

            if (EditorGUI.EndChangeCheck() && stateFunctionIdx >= 0)
            {
                var stateFunction = functions[stateFunctionIdx];
                property.objectReferenceValue = stateFunction;
            }
            EditorGUI.EndProperty();
        }

        private void RefreshStateFunctionList()
        {
            if (EditorApplication.timeSinceStartup - lastRefresh < 3f)
                return;

            lastRefresh = EditorApplication.timeSinceStartup;

            functions.Clear();
            foreach (var asset in Utils.FindAssetsByType<StateFunction>())
            {
                functions.Add(asset);
            }
            if (funcNames?.Length != functions.Count)
                funcNames = new string[functions.Count];

            for (int i = 0; i < functions.Count; i++)
                funcNames[i] = functions[i].name;
        }
    }
}