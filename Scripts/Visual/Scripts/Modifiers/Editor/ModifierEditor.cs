using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Reflection;
using System.Collections;
using Unity.EditorCoroutines.Editor;

namespace OneHamsa.Dexterity.Visual
{
    [CustomEditor(typeof(Modifier), true)]
    public class ModifierEditor : TransitionBehaviourEditor
    {
        static Dictionary<string, bool> foldedStates = new Dictionary<string, bool>();
        bool strategyExists { get; set; }
        Modifier modifier { get; set; }
        List<SerializedProperty> stateProps = new List<SerializedProperty>(8);
        private EditorCoroutine coro;

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
                    EditorUtility.SetDirty(target);
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
            if (stateFunction == null)
            {
                EditorGUILayout.HelpBox("Must select State Function for node", MessageType.Error);
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
                var attr = objType.GetCustomAttribute<ModifierPropertyDefinitionAttribute>(true);
                propType = attr?.propertyType
                    ?? (!string.IsNullOrEmpty(attr.propertyName) ? objType.GetNestedType(attr.propertyName) : null);
                    
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

                    if (GUILayout.Button(EditorGUIUtility.IconContent("d_PlayButton"),
                        GUILayout.Width(25)))
                    {
                        if (coro != null)
                            EditorCoroutineUtility.StopCoroutine(coro);
                        coro = EditorCoroutineUtility.StartCoroutine(
                            AnimateStateTransition(modifier.node, new Modifier[] { modifier }, propState), this);
                    }
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

        public static IEnumerator AnimateStateTransition(Node node, IEnumerable<Modifier> modifiers, 
        string state, float speed = 1f)
        {
            // record all components on modifiers for undo
            foreach (var modifier in modifiers) {
                Undo.RegisterCompleteObjectUndo(modifier.GetComponents<Component>().ToArray(), "Editor Transition");
            }
            Undo.FlushUndoRecordObjects();

            // setup
            Manager.instance.RegisterStateFunction(node.stateFunctionAsset);
            foreach (var modifier in modifiers)
                modifier.Awake();

            if (node.activeState == -1)
                node.activeState = Manager.instance.GetStateID(state);

            var startTime = EditorApplication.timeSinceStartup;
            node.currentTime = startTime;
            node.stateChangeTime = EditorApplication.timeSinceStartup;

            foreach (var modifier in modifiers) {
                modifier.HandleNodeEnabled();
                // force updating at least once
                modifier.ForceTransitionUpdate();
            }

            node.activeState = Manager.instance.GetStateID(state);

            do {
                // immitate a frame
                yield return null;
                node.currentTime = startTime + (EditorApplication.timeSinceStartup - startTime) * speed;

                // transition
                foreach (var modifier in modifiers) {
                    modifier.Update();
                    EditorUtility.SetDirty(modifier);
                }
            } while (modifiers.Any(m => m.IsChanged()));

            // cleanup
            Manager.instance.Reset();
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
