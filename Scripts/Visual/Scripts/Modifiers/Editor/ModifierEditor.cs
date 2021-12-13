using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Reflection;

namespace OneHamsa.Dexterity.Visual
{
    [CustomEditor(typeof(Modifier), true)]
    public class ModifierEditor : TransitionBehaviourEditor
    {
        static Dictionary<string, bool> foldedStates = new Dictionary<string, bool>();
        bool strategyExists { get; set; }
        Modifier modifier { get; set; }
        List<SerializedProperty> stateProps = new List<SerializedProperty>(8);
        private bool propertiesUpdated { get; set; } 

        private void OnEnable() 
        {
            modifier = target as Modifier;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            propertiesUpdated = false;

            ShowNode();

            var customProps = new List<SerializedProperty>();
            var parent = serializedObject.GetIterator();
            foreach (var prop in Utils.GetVisibleChildren(parent))
            {
                switch (prop.name)
                {
                    case "m_Script":
                        break;
                    case nameof(Modifier._node):
                        // separate
                        break;
                    case nameof(Modifier.properties):
                        // show later
                        break;
                    case nameof(Modifier.transitionStrategy):
                        strategyExists = ShowStrategy();
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
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(prop, true);
                    propertiesUpdated |= EditorGUI.EndChangeCheck();
                }

                if (modifier is ISupportValueFreeze valueFreeze && GUILayout.Button("Freeze Values"))
                {
                    valueFreeze.FreezeValue();
                }
            }

            var stateFunction = modifier?.node?.stateFunctionAsset;
            if (stateFunction != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("States", EditorStyles.whiteLargeLabel);
                propertiesUpdated |= ShowProperties(stateFunction);
            }

            // warnings
            if (modifier.node != null && modifier.node.referenceAssets.Count(a => a != null) == 0)
            {
                EditorGUILayout.HelpBox("Must select Node Reference(s) for node", MessageType.Error);
            }
            if (!strategyExists)
            {
                var strategyProp = serializedObject.FindProperty(nameof(Modifier.transitionStrategy));
                var className = Utils.GetClassName(strategyProp);
                var types = TypeCache.GetTypesDerivedFrom<ITransitionStrategy>();
                var typesNames = types
                    .Select(t => t.ToString())
                    .ToArray();

                /*
                if (string.IsNullOrEmpty(className))
                {
                    var currentIdx = Array.IndexOf(typesNames, modifier.node?.referenceAsset?.defaultStrategy);
                    if (currentIdx != -1)
                        strategyProp.managedReferenceValue = Activator.CreateInstance(types[currentIdx]);
                }
                */

                EditorGUILayout.HelpBox("Must select Transition Strategy", MessageType.Error);
            }

            serializedObject.ApplyModifiedProperties();

            if (propertiesUpdated)
                modifier.ForceTransitionUpdate();
        }

        private void ShowNode()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(Modifier._node)));
            var helpboxStyle = new GUIStyle(EditorStyles.helpBox);
            helpboxStyle.richText = true;

            if (modifier._node == null)
            {
                if (modifier.node == null)
                {
                    EditorGUILayout.HelpBox($"Could not find parent node, select manually or fix hierarchy",
                        MessageType.Error);
                }
                else
                    if (GUILayout.Button($"Automatically selecting parent (<b><color=cyan>{modifier.node.name}</color></b>)",
                        helpboxStyle))
                {
                    EditorGUIUtility.PingObject(modifier.node);
                }
            }
        }

        bool ShowProperties(StateFunctionGraph sf)
        {
            var updated = false;

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

            var activeState = (target as Modifier).node.activeState;

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

                void UtilityButtons()
                {
                    if (modifier is ISupportPropertyFreeze propFreeze
                        && GUILayout.Button(EditorGUIUtility.IconContent("RotateTool On", "Freeze"), GUILayout.Width(25)))
                    {
                        Undo.RecordObject(modifier, "Freeze value");
                        propFreeze.FreezeProperty(modifier.properties[i]);
                    }

                    /*
                    if (GUILayout.Button(EditorGUIUtility.IconContent("d_PlayButton"),
                        GUILayout.Width(25)))
                    {

                    }
                    */
                }

                if (stateProps.Count > 1)
                {
                    // multiple - fold
                    EditorGUILayout.BeginHorizontal();
                    foldedStates[propState] = EditorGUILayout.Foldout(foldedStates[propState],
                        $"{propState}{suffix}", true, EditorStyles.foldoutHeader);
                    GUI.contentColor = origColor;

                    UtilityButtons();

                    EditorGUILayout.EndHorizontal();

                    // show fields
                    if (foldedStates[propState])
                        foreach (var field in stateProps)
                        {
                            EditorGUI.BeginChangeCheck();
                            EditorGUILayout.PropertyField(field);
                            updated |= EditorGUI.EndChangeCheck();
                        }
                }
                else if (stateProps.Count == 1)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"{propState}{suffix}");
                    GUI.contentColor = origColor;

                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(stateProps[0], new GUIContent());
                    updated |= EditorGUI.EndChangeCheck();

                    UtilityButtons();

                    EditorGUILayout.EndHorizontal();
                }
            }
            DrawSeparator();

            if (Application.isPlaying) // debug view
                Repaint();

            return updated;
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
