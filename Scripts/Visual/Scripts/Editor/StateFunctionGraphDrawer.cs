using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    [CustomPropertyDrawer(typeof(StateFunctionGraph))]
    public class StateFunctionGraphDrawer : PropertyDrawer
    {
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

            var functions = Utils.FindAssetsByType<StateFunctionGraph>().ToList();
            var funcNames = functions.Select(f => f.name).ToArray();

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
    }
}