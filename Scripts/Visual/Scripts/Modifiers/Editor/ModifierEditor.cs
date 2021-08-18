using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Reflection;

namespace OneHamsa.Dexterity.Visual
{
    [CustomEditor(typeof(Modifier), true)]
    public class ModifierEditor : Editor
    {
        StateFunction stateFunctionObj = null;
        int stateFunctionIdx;
        static Dictionary<string, bool> foldedStates = new Dictionary<string, bool>();
        bool strategyDefined;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var functions = Manager.Instance.stateFunctions.Where(f => f != null).Select(f => f.name).ToArray();
            var stateFunctionProperty = serializedObject.FindProperty(nameof(Modifier.stateFunction));

            var customProps = new List<SerializedProperty>();
            var parent = serializedObject.GetIterator();
            foreach (var prop in Utils.GetVisibleChildren(parent))
            {
                switch (prop.name)
                {
                    case "m_Script":
                        break;
                    case "node":
                        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(Modifier.node)));
                        EditorGUI.indentLevel++;
                        EditorGUILayout.LabelField("Leave empty for parent", EditorStyles.miniLabel);
                        EditorGUI.indentLevel--;
                        break;

                    case "stateFunction":
                        stateFunctionObj = (StateFunction)stateFunctionProperty.objectReferenceValue;

                        
                        stateFunctionIdx = EditorGUILayout.Popup("State Function",
                            Array.IndexOf(functions, stateFunctionObj?.name), functions);
                        break;
                    case "properties":
                        // show later
                        break;
                    case "transitionStrategy":
                        strategyDefined = ShowStrategy();
                        break;

                    default:
                        // get all custom properties here
                        customProps.Add(prop.Copy());
                        break;
                }
            }

            // show custom props
            if (customProps.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Modifier Parameters", EditorStyles.whiteLargeLabel);
                foreach (var prop in customProps)
                    EditorGUILayout.PropertyField(prop, true);
            }

            // finally show states
            if (stateFunctionIdx >= 0)
            {
                var stateFunction = Manager.Instance.GetStateFunction(functions[stateFunctionIdx]);
                stateFunctionProperty.objectReferenceValue = stateFunction;

                if (stateFunction != null)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("States", EditorStyles.whiteLargeLabel);
                    ShowProperties(stateFunction);
                }
            }

            // warnings
            if (stateFunctionObj == null)
            {
                var origColor = GUI.color;
                GUI.color = Color.red;
                EditorGUILayout.LabelField("Must select State Function", EditorStyles.helpBox);
                GUI.color = origColor;
            }
            if (!strategyDefined)
            {
                var origColor = GUI.color;
                GUI.color = Color.red;
                EditorGUILayout.LabelField("Must select Transition Strategy", EditorStyles.helpBox);
                GUI.color = origColor;
            }

            serializedObject.ApplyModifiedProperties();
        }

        void ShowProperties(StateFunction sf)
        {
            sf.InvalidateCache();

            // get property info, iterate through parent classes to support inheritance
            Type propType = null;
            var objType = target.GetType();
            while (propType == null)
            {
                propType = objType.GetNestedType("Property");
                objType = objType.BaseType;
            }

            var properties = serializedObject.FindProperty(nameof(Modifier.properties));
            var defaultState = serializedObject.FindProperty(nameof(Modifier.defaultState));
            var states = sf.GetStates();

            // find all existing references to properties by state name, add more entries if needed
            var currentPropStates = new HashSet<string>();
            for (var i = 0; i < properties.arraySize; ++i)
            {
                var property = properties.GetArrayElementAtIndex(i);
                var propState = property?.FindPropertyRelative(nameof(Modifier.PropertyBase.state))?.stringValue;
                currentPropStates.Add(propState);
            }

            foreach (var state in states)
            {
                if (!currentPropStates.Contains(state))
                {
                    var last = properties.arraySize;
                    properties.arraySize++;
                    var newElement = properties.GetArrayElementAtIndex(last);
                    newElement.managedReferenceValue = Activator.CreateInstance(propType);
                    newElement.FindPropertyRelative(nameof(Modifier.PropertyBase.state)).stringValue = state;
                }

                if (!foldedStates.ContainsKey(state))
                    foldedStates[state] = true;
            }

            if (string.IsNullOrEmpty(defaultState.stringValue) && states.Count > 0)
            {
                var first = states.First();
                Debug.LogWarning($"no default state selected, selecting first ({first})", target);
                defaultState.stringValue = first;
            }

            var activeState = (target as Modifier).activeState;

            // draw the editor for each value in property
            for (var i = 0; i < properties.arraySize; ++i)
            {
                var property = properties.GetArrayElementAtIndex(i);
                var propState = property.FindPropertyRelative(nameof(Modifier.PropertyBase.state)).stringValue;

                if (!states.Contains(propState))
                    continue;

                DrawSeparator();

                EditorGUILayout.BeginHorizontal();
                // name 
                var origColor = GUI.contentColor;
                var suffix = "";
                if (activeState == propState)
                {
                    GUI.contentColor = Color.green;
                    suffix = " (current)";
                }
                foldedStates[propState] = EditorGUILayout.Foldout(foldedStates[propState], 
                    $"{propState}{suffix}", true, EditorStyles.foldoutHeader);
                GUI.contentColor = origColor;
                GUILayout.FlexibleSpace();

                // default?
                if (GUILayout.Toggle(propState == defaultState.stringValue, 
                    new GUIContent("", $"default ({propState})?"))) {
                    defaultState.stringValue = propState;
                }
                EditorGUILayout.EndHorizontal();

                // fields
                if (foldedStates[propState]) 
                    foreach (var field in Utils.GetChildren(property))
                    {
                        if (field.name == "State")
                            continue;
                        EditorGUILayout.PropertyField(field);
                    }
            }
            DrawSeparator();

            // XXX only call when some debug menu is open?
            Repaint();
        }

        bool ShowStrategy()
        {
            var strategyProp = serializedObject.FindProperty(nameof(Modifier.transitionStrategy));
            var className = Utils.GetClassName(strategyProp);

            var types = Utils.GetSubtypes<ITransitionStrategy>();
            var typesNames = types
                .Select(t => t.ToString())
                .ToArray();

            var currentIdx = Array.IndexOf(typesNames, className);
            var fieldIdx = EditorGUILayout.Popup("Transition Strategy", currentIdx,
                Utils.GetNiceName(typesNames, suffix: "Strategy").ToArray());

            if (fieldIdx >= 0 && currentIdx != fieldIdx)
            {
                var type = types[fieldIdx];
                strategyProp.managedReferenceValue = Activator.CreateInstance(type);
            }

            if (!string.IsNullOrEmpty(className))
                EditorGUILayout.PropertyField(strategyProp, new GUIContent("Strategy Parameters"), true);

            return !string.IsNullOrEmpty(className);
        }

        static void DrawSeparator()
        {
            EditorGUILayout.Space();
            var rect = EditorGUILayout.BeginHorizontal();
            Handles.color = Color.gray;
            Handles.DrawLine(new Vector2(rect.x - 15, rect.y), new Vector2(rect.width + 15, rect.y));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }
    }
}
