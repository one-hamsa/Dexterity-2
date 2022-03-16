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
    [CustomEditor(typeof(Modifier), true), CanEditMultipleObjects]
    public class ModifierEditor : TransitionBehaviourEditor
    {
        static Dictionary<string, bool> foldedStates = new Dictionary<string, bool>();
        bool strategyExists { get; set; }
        Modifier modifier { get; set; }
        List<SerializedProperty> stateProps = new List<SerializedProperty>(8);
        private EditorCoroutine coro;
        private List<SerializedProperty> customProps = new List<SerializedProperty>();
        private HashSet<string> currentPropStates = new HashSet<string>();
        private HashSet<string> sfStates = new HashSet<string>();

        private bool propertiesUpdated { get; set; } 

        private void OnEnable() 
        {
            modifier = target as Modifier;
        }

        public override void OnInspectorGUI()
        {
            sfStates.Clear();
            var first = true;
            foreach (var t in targets) {
                foreach (var state in (t as IStatesProvider).GetStateNames()) {
                    if (sfStates.Add(state) && !first) {
                        EditorGUILayout.HelpBox("Can't multi-edit modifiers with different state lists.", MessageType.Error);
                        return;
                    }
                }
                first = false;
            }

            serializedObject.Update();

            propertiesUpdated = false;

            ShowNode();

            customProps.Clear();
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
                        var p = serializedObject.FindProperty(nameof(Modifier.transitionStrategy));
                        strategyExists = ShowStrategy(target, p);
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

                if (targets.Length == 1 &&
                    modifier is ISupportValueFreeze valueFreeze && GUILayout.Button("Freeze Values"))
                {
                    Undo.RecordObject(modifier, "Freeze value");
                    valueFreeze.FreezeValue();
                }
            }

            if (sfStates.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("States", EditorStyles.whiteLargeLabel);
                propertiesUpdated |= ShowProperties(sfStates);
            }

            // warnings
            if (sfStates.Count == 0)
            {
                EditorGUILayout.HelpBox("Node has no states", MessageType.Error);
            }
            if (!strategyExists)
            {
                var strategyProp = serializedObject.FindProperty(nameof(Modifier.transitionStrategy));
                var className = Utils.GetClassName(strategyProp);
                var types = TypeCache.GetTypesDerivedFrom<ITransitionStrategy>();
                var typesNames = types
                    .Select(t => t.ToString())
                    .ToArray();

                EditorGUILayout.HelpBox("Must select Transition Strategy", MessageType.Error);
            }
            
            if (targets.Length > 1) 
            {
                EditorGUILayout.HelpBox($"Some options are hidden in multi-edit mode", MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();

            if (propertiesUpdated) {
                foreach (var target in targets)
                    (target as Modifier).ForceTransitionUpdate();
            }
        }

        private void ShowNode()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(Modifier._node)));
            var helpboxStyle = new GUIStyle(EditorStyles.helpBox);
            helpboxStyle.richText = true;

            if (targets.Length > 1)
                return;

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

        bool ShowProperties(IEnumerable<string> states)
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

            // find all existing references to properties by state name, add more entries if needed
            currentPropStates.Clear();
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
                    
                    var newProp = Activator.CreateInstance(propType) as Modifier.PropertyBase;
                    newProp.state = state;
                    if (modifier is ISupportPropertyFreeze supportPropertyFreeze) {
                        supportPropertyFreeze.FreezeProperty(newProp);
                    }

                    newElement.managedReferenceValue = newProp;
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
                if (Application.isPlaying && activeState == Core.instance.GetStateID(propState))
                {
                    GUI.contentColor = Color.green;
                    suffix = " (current)";
                }

                void UtilityButtons()
                {
                    if (targets.Length > 1)
                        return; 

                    if (modifier is ISupportPropertyFreeze propFreeze
                        && GUILayout.Button(EditorGUIUtility.IconContent("RotateTool On", "Freeze"), GUILayout.Width(25)))
                    {
                        Undo.RecordObject(modifier, "Freeze value");
                        propFreeze.FreezeProperty(modifier.properties[i]);
                    }

                    GUI.enabled = Application.isPlaying;
                    if (GUILayout.Button(EditorGUIUtility.IconContent("d_PlayButton"),
                        GUILayout.Width(25)))
                    {
                        if (coro != null)
                            EditorCoroutineUtility.StopCoroutine(coro);
                        coro = EditorCoroutineUtility.StartCoroutine(
                            AnimateStateTransition(modifier.node, new Modifier[] { modifier }, propState), this);
                    }
                    GUI.enabled = true;
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

            // destroy previous instance, it's ok because it's editor time
            if (Core.instance != null)
                Core.Destroy();
            Core.Create(DexteritySettingsProvider.settings);

            // setup
            foreach (var asset in node.GetStateFunctionAssetsIncludingReferences())
                Core.instance.RegisterStateFunction(asset);

            foreach (var modifier in modifiers)
                modifier.Awake();

            // if it's the first run (didn't run an editor transition before), reset to same state (won't show animation)
            if (node.activeState == -1)
                node.activeState = Core.instance.GetStateID(state);

            var startTime = EditorApplication.timeSinceStartup;
            node.currentTime = startTime;
            node.stateChangeTime = EditorApplication.timeSinceStartup;

            foreach (var modifier in modifiers) {
                modifier.HandleNodeEnabled();
                // force updating at least once
                modifier.ForceTransitionUpdate();
            }

            node.activeState = Core.instance.GetStateID(state);

            bool anyChanged;

            do {
                // immitate a frame
                yield return null;
                node.currentTime = startTime + (EditorApplication.timeSinceStartup - startTime) * speed;

                // transition
                anyChanged = false;
                foreach (var modifier in modifiers) {
                    modifier.Update();
                    if (modifier.IsChanged()) {
                        anyChanged = true;
                        EditorUtility.SetDirty(modifier);
                    }
                }
                if (anyChanged) {
                    SceneView.RepaintAll();
                }
            } while (anyChanged);

            Core.Destroy();
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
