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
        static Dictionary<string, bool> foldedStates = new();
        bool strategyExists { get; set; }
        Modifier modifier { get; set; }
        List<SerializedProperty> stateProps = new(8);
        private EditorCoroutine coro;
        private List<SerializedProperty> customProps = new();
        private List<(string stateName, SerializedProperty prop, int index)> sortedStateProps = new();
        private bool hasUpdateOverride;

        private bool propertiesUpdated { get; set; } 

        private void OnEnable() 
        {
            modifier = (Modifier)target;
            
            var myUpdateType = modifier.GetType() 
                .GetMethod(nameof(Modifier.Refresh), 
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                .DeclaringType;
            
            var baseUpdateType = typeof(Modifier).GetMethod(nameof(Modifier.Refresh), 
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                .DeclaringType;
            
            hasUpdateOverride = myUpdateType != baseUpdateType;
        }

        public override void OnInspectorGUI()
        {
            var alphabetically = ((IHasStates)target).GetStateNames().OrderBy(x => x).ToList();
            var states = alphabetically.ToHashSet();
            
            foreach (var m in targets.Cast<Modifier>())
            {
                var targetStates = m.properties.Select(p => p?.state).ToList();
                var changed = false;
                foreach (var state in alphabetically)
                {
                    if (!targetStates.Contains(state))
                    {
                        AddStateToModifier(m, state);
                        changed = true;
                    }
                }
                
                foreach (var state in targetStates)
                {
                    if (state == null || !alphabetically.Contains(state))
                    {
                        RemoveStateFromModifier(m, state);
                        changed = true;
                    }
                }

                if (changed)
                {
                    serializedObject.Update();
                    targetStates = m.properties.Select(p => p.state).ToList();
                }
                
                if (targetStates.Count != alphabetically.Count || !targetStates.ToHashSet().SetEquals(states)) {
                    EditorGUILayout.HelpBox("Can't multi-edit modifiers with different state lists.", MessageType.Error);
                    return;
                }
                if (!targetStates.SequenceEqual(alphabetically))
                {
                    m.properties = m.properties.OrderBy(x => x.state).ToList();
                    EditorUtility.SetDirty(target);
                }
            }

            serializedObject.Update();
            
            propertiesUpdated = false;

            ShowNode(states);

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
                        if (hasUpdateOverride)
                        {
                            var p = serializedObject.FindProperty(nameof(Modifier.transitionStrategy));
                            strategyExists = ShowStrategy(target, p);
                        }

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

            if (states.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("States", EditorStyles.whiteLargeLabel);
                propertiesUpdated |= ShowProperties(states);

                if (targets.Length == 1 && 
                    modifier is ISupportPropertyFreeze propFreeze && GUILayout.Button("Freeze Properties")) 
                {
                    Undo.RecordObject(modifier, "Freeze properties");
                    foreach (var prop in modifier.properties) {
                        propFreeze.FreezeProperty(prop);
                    }
                }
            }

            // warnings
            if (states.Count == 0)
            {
                EditorGUILayout.HelpBox("Node has no states", MessageType.Error);
            }
            if (!strategyExists && hasUpdateOverride)
            {
                EditorGUILayout.HelpBox("Must select Transition Strategy", MessageType.Error);
            }
            
            if (targets.Length > 1) 
            {
                EditorGUILayout.HelpBox($"Some options are hidden in multi-edit mode", MessageType.Warning);
            }

            serializedObject.ApplyModifiedProperties();

            if (propertiesUpdated && Application.isPlaying) {
                foreach (var target in targets)
                    (target as Modifier).ForceTransitionUpdate();
            }
        }

        private void AddStateToModifier(Modifier m, string state)
        {
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

            var newProp = (Modifier.PropertyBase)Activator.CreateInstance(propType);
            newProp.state = state;
            m.properties.Add(newProp);
            if (m is ISupportPropertyFreeze supportPropertyFreeze) 
                supportPropertyFreeze.FreezeProperty(newProp);
            EditorUtility.SetDirty(target);
        }
        
        private void RemoveStateFromModifier(Modifier m, string state)
        {
            m.properties.RemoveAll(p => p?.state == state);
            EditorUtility.SetDirty(target);
        }


        private void ShowNode(HashSet<string> stateNames)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(Modifier._node)));
            var helpboxStyle = new GUIStyle(EditorStyles.helpBox);
            helpboxStyle.richText = true;

            if (targets.Length > 1)
                return;

            var hasNode = true;
            if (modifier._node == null)
            {
                if (modifier.GetNode() == null)
                {
                    hasNode = false;
                    EditorGUILayout.HelpBox($"Could not find parent node, showing latest states found",
                        MessageType.Warning);
                }
                else
                    if (GUILayout.Button($"Automatically selecting parent (<b><color=cyan>{modifier.GetNode().name}</color></b>)",
                        helpboxStyle))
                {
                    EditorGUIUtility.PingObject(modifier.GetNode());
                }
            }

            if (hasNode)
            {
                // move from node to modifier
                modifier.lastSeenStates.Clear();
                foreach (var state in stateNames)
                    modifier.lastSeenStates.Add(state);
            }
            else
            {
                // use cache
                stateNames.Clear();
                foreach (var state in modifier.lastSeenStates)
                    stateNames.Add(state);
            }
        }

        bool ShowProperties(IEnumerable<string> states)
        {
            var updated = false;

            var statesList = states.ToList();
            foreach (var state in statesList)
            {
                if (!foldedStates.ContainsKey(state))
                    foldedStates[state] = true;
            }
            
            var properties = serializedObject.FindProperty(nameof(Modifier.properties));
            sortedStateProps.Clear();
            for (var i = 0; i < properties.arraySize; ++i)
            {
                var property = properties.GetArrayElementAtIndex(i);
                var propState = property.FindPropertyRelative(nameof(Modifier.PropertyBase.state)).stringValue;
                sortedStateProps.Add((propState, property, i));
            }

            var node = ((Modifier)target).GetNode();

            // draw the editor for each value in property
            foreach (var (propState, property, i) in sortedStateProps)
            {
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
                if (Application.isPlaying)
                {
                    var stateId = Database.instance.GetStateID(propState);
                    if (stateId != StateFunction.emptyStateId)
                    {
                        if (modifier.GetActiveState() == stateId)
                        {
                            GUI.contentColor = Color.green;
                            suffix = $" (current, {Mathf.RoundToInt(modifier.transitionProgress * 100)}%)";
                        }
                        else
                        {
                            suffix = $" ({Mathf.RoundToInt(modifier.GetTransitionProgress(stateId) * 100)}%)";
                        }
                    }
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

                    if (Application.isPlaying)
                        return;

                    GUI.contentColor = coro != null ? Color.green : origColor;
                    GUI.enabled = modifier.animatableInEditor;
                    if (GUILayout.Button(EditorGUIUtility.IconContent("d_PlayButton"),
                        GUILayout.Width(25)))
                    {
                        void Animate(float speed) {
                            if (coro != null)
                                EditorCoroutineUtility.StopCoroutine(coro);
                            coro = EditorCoroutineUtility.StartCoroutine(
                                AnimateStateTransition(modifier.GetNode(), new Modifier[] { modifier }, propState, speed, () => coro = null), this);
                        }

                        if (Event.current.button == 1) {
                            // right click
                            var menu = new GenericMenu();
                            foreach (var speed in new [] { .1f, .25f, .5f, 1f, 1.25f, 1.5f, 2f })
                            {
                                menu.AddItem(new GUIContent($"x{speed}"), false, () => Animate(speed));
                            }
                            menu.ShowAsContext();
                        } else {
                            Animate(1f);
                        }
                    }
                    GUI.contentColor = origColor;
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
                    EditorGUILayout.LabelField($"{propState}{suffix}", GUILayout.MinWidth(30));
                    GUI.contentColor = origColor;

                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(stateProps[0], GUIContent.none, GUILayout.MinWidth(30));
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

        public static IEnumerator AnimateStateTransition(DexterityBaseNode node, IEnumerable<Modifier> modifiers, 
        string state, float speed = 1f, Action onEnd = null)
        {
            modifiers = modifiers.ToList();
            // make sure it's not called with non-animatable modifiers
            if (modifiers.Any(m => !m.animatableInEditor))
            {
                Debug.LogError($"{nameof(AnimateStateTransition)} called with non-animatable modifiers");
                yield break;
            }

            // record all components on modifiers for undo
            foreach (var modifier in modifiers) {
                Undo.RegisterCompleteObjectUndo(modifier.GetComponents<Component>().ToArray(), "Editor Transition");
            }
            Undo.FlushUndoRecordObjects();
            
            var animationContexts = modifiers.Select(m => m.GetEditorAnimationContext()).ToList();
            try
            {

                // destroy previous instance, it's ok because it's editor time
                Database.Destroy();
                using var db = Database.Create(DexteritySettingsProvider.settings);

                // timeScale doesn't behave nicely in editor
                //Core.instance.timeScale = speed;

                // setup
                db.Register(node);
                if (node is IStepList stepList)
                    stepList.InitializeSteps();

                foreach (var ctx in animationContexts)
                    ctx.Initialize();

                if (
                    // it's the first run (didn't run an editor transition before)
                    node.GetActiveState() == StateFunction.emptyStateId
                    // activeState might be invalid at that point
                    || !node.GetStateIDs().Contains(node.GetActiveState()))
                {
                    // reset to initial state 
                    node.SetActiveState_Editor(db.GetStateID(node.initialState));
                }

                node.timeSinceStateChange = 0f;

                foreach (var ctx in animationContexts)
                    ctx.OnNodeEnabled();

                var oldState = node.GetActiveState();
                node.SetActiveState_Editor(db.GetStateID(state));
                foreach (var ctx in animationContexts)
                    ctx.HandleStateChange(oldState, node.GetActiveState());

                bool anyChanged;

                var lastUpdate = EditorApplication.timeSinceStartup;
                do
                {
                    // immitate a frame
                    yield return null;

                    // stop if something went wrong
                    if (Database.instance == null)
                        break;

                    node.deltaTime = (EditorApplication.timeSinceStartup - lastUpdate) * speed;
                    node.timeSinceStateChange += node.deltaTime;
                    lastUpdate = EditorApplication.timeSinceStartup;

                    // transition
                    anyChanged = false;
                    foreach (var ctx in animationContexts)
                    {
                        // guard for async changes that might result in reference loss
                        if (!ctx.IsValid())
                            continue;

                        anyChanged |= ctx.Refresh();
                    }

                    if (anyChanged)
                    {
                        SceneView.RepaintAll();
                    }
                } while (anyChanged);
            }
            finally
            {
                foreach (var ctx in animationContexts)
                    ctx.Dispose();
            }

            Database.Destroy();
            onEnd?.Invoke();
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
