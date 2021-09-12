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
        static Dictionary<string, bool> foldedStates = new Dictionary<string, bool>();
        bool strategyDefined;
        Modifier modifier;
        List<SerializedProperty> stateProps = new List<SerializedProperty>(8);
        private bool advancedFoldout;

        private void OnEnable()
        {
            modifier = target as Modifier;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var customProps = new List<SerializedProperty>();
            var parent = serializedObject.GetIterator();
            foreach (var prop in Utils.GetVisibleChildren(parent))
            {
                switch (prop.name)
                {
                    case "m_Script":
                        break;
                    case nameof(Modifier._node):
                        EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(Modifier._node)));
                        EditorGUI.indentLevel++;
                        EditorGUILayout.LabelField($"Automatically selecting parent ({modifier.node.name})", 
                            EditorStyles.miniLabel);
                        EditorGUI.indentLevel--;
                        break;
                    case nameof(Modifier.properties):
                        // show later
                        break;
                    case nameof(Modifier.transitionStrategy):
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

            // show state function button (play time)
            if (Application.isPlaying)
            {
                if (GUILayout.Button("State Function Live View"))
                {
                    EditorWindow.GetWindow<StateFunctionGraphWindow>().InitializeGraph(modifier.activeStateFunction);
                }
            }

            var stateFunction = modifier.node.referenceAsset?.stateFunction;
            if (stateFunction != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("States", EditorStyles.whiteLargeLabel);
                ShowProperties(stateFunction);
            }

            // warnings
            if (modifier.node.referenceAsset == null)
            {
                var origColor = GUI.color;
                GUI.color = Color.yellow;
                EditorGUILayout.LabelField("Must select Node Reference for node", EditorStyles.helpBox);
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
        
        void ShowProperties(StateFunctionGraph sf)
        {
            // get property info, iterate through parent classes to support inheritance
            Type propType = null;
            var objType = target.GetType();
            while (propType == null)
            {
                propType = objType.GetNestedType("Property");
                objType = objType.BaseType;
            }

            var properties = serializedObject.FindProperty(nameof(Modifier.properties));
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

            var activeState = (target as Modifier).activeState;

            // draw the editor for each value in property
            for (var i = 0; i < properties.arraySize; ++i)
            {
                var property = properties.GetArrayElementAtIndex(i);
                var propState = property.FindPropertyRelative(nameof(Modifier.PropertyBase.state)).stringValue;

                if (!states.Contains(propState))
                    continue;

                DrawSeparator();

                stateProps.Clear();

                // fields
                foreach (var field in Utils.GetChildren(property))
                {
                    if (field.name == nameof(Modifier.PropertyBase.state))
                        continue;

                    stateProps.Add(field.Copy());
                }

                // name 
                var origColor = GUI.contentColor;
                var suffix = "";
                if (Application.isPlaying && activeState == Manager.instance.GetStateID(propState))
                {
                    GUI.contentColor = Color.green;
                    suffix = " (current)";
                }

                if (stateProps.Count > 1)
                {
                    // multiple - fold
                    EditorGUILayout.BeginHorizontal();
                    foldedStates[propState] = EditorGUILayout.Foldout(foldedStates[propState],
                        $"{propState}{suffix}", true, EditorStyles.foldoutHeader);
                    GUI.contentColor = origColor;
                    EditorGUILayout.EndHorizontal();

                    // show fields
                    if (foldedStates[propState])
                        foreach (var field in stateProps)
                            EditorGUILayout.PropertyField(field);
                }
                else if (stateProps.Count == 1)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"{propState}{suffix}");
                    GUI.contentColor = origColor;
                    EditorGUILayout.PropertyField(stateProps[0], new GUIContent());
                    EditorGUILayout.EndHorizontal();
                }
            }
            DrawSeparator();

            if (Application.isPlaying) // debug view
                Repaint();
        }

        bool ShowStrategy()
        {
            var strategyProp = serializedObject.FindProperty(nameof(Modifier.transitionStrategy));
            var className = Utils.GetClassName(strategyProp);

            if (!string.IsNullOrEmpty(className))
            {
                foreach (var field in Utils.GetChildren(strategyProp))
                {
                    EditorGUILayout.PropertyField(field);
                }
            }


            var saveStrategy = false;
            var types = Utils.GetSubtypes<ITransitionStrategy>();
            var typesNames = types
                .Select(t => t.ToString())
                .ToArray();

            var currentIdx = Array.IndexOf(typesNames, className);
            if (currentIdx == -1)
            {
                currentIdx = Array.IndexOf(typesNames, modifier.node.referenceAsset.defaultStrategy);
                saveStrategy = true;
            }
            var fieldIdx = currentIdx;

            if (advancedFoldout = EditorGUILayout.Foldout(advancedFoldout, "Advanced"))
            {
                EditorGUI.BeginChangeCheck();
                fieldIdx = EditorGUILayout.Popup("Transition Strategy", currentIdx,
                    Utils.GetNiceName(typesNames, suffix: "Strategy").ToArray());

                if (EditorGUI.EndChangeCheck())
                {
                    saveStrategy = true;
                }
            }
            if (saveStrategy)
            {
                var type = types[fieldIdx];
                strategyProp.managedReferenceValue = Activator.CreateInstance(type);
            }

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
