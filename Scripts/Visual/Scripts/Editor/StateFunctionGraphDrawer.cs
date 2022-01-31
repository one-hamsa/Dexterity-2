using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    [CustomPropertyDrawer(typeof(StateFunctionGraph))]
    public class StateFunctionGraphDrawer : PropertyDrawer
    {
        private List<StateFunctionGraph> functions = new List<StateFunctionGraph>();
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
                EditorGUI.LabelField(position, label.text, "Use [StateFunctionGraph] with StateFunctionGraph.");
                return;
            }

            RefreshStateFunctionList();
            var stateFunctionObj = (StateFunctionGraph)property.objectReferenceValue;

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

            r.y += EditorGUIUtility.singleLineHeight;
            r.yMax = position.yMax;

            if (stateFunctionObj != null && GUI.Button(r, "Open State Function Graph"))
            {
                EditorWindow.GetWindow<StateFunctionGraphWindow>().InitializeGraph(stateFunctionObj);
            }
        }

        private void RefreshStateFunctionList()
        {
            if (EditorApplication.timeSinceStartup - lastRefresh < 3f)
                return;

            lastRefresh = EditorApplication.timeSinceStartup;

            functions.Clear();
            foreach (var asset in Utils.FindAssetsByType<StateFunctionGraph>())
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