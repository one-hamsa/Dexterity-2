using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Reflection;
using Unity.EditorCoroutines.Editor;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace OneHamsa.Dexterity
{
    [CustomEditor(typeof(Modifier), true), CanEditMultipleObjects]
    public class ModifierEditor : TransitionBehaviourEditor
    {
        static Dictionary<string, bool> foldedStates = new();
        bool strategyExists { get; set; }
        protected Modifier modifier => (Modifier)target;
        private EditorCoroutine coro;
        // private  sortedStateProps = new();
        private bool hasUpdateOverride;
        private string lastAnimatedState;
        private bool propertiesUpdated { get; set; } 

        private void OnEnable() 
        {
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
            if (targets.Length == 1)
            {
                var (comment, type) = modifier.GetEditorComment();
                if (!string.IsNullOrEmpty(comment))
                {
                    EditorGUILayout.HelpBox(comment, type switch
                    {
                        LogType.Error => MessageType.Error,
                        LogType.Assert => MessageType.Error,
                        LogType.Exception => MessageType.Error,
                        LogType.Warning => MessageType.Warning,
                        _ => MessageType.Info
                    });
                }
            }
            
            var alphabetically = ((IHasStates)target).GetStateNames().OrderBy(x => x).ToList();
            var states = alphabetically.ToHashSet();
            
            foreach (var m in targets.Cast<Modifier>())
            {
                var node = m.GetNode();
                if (node != null && node.ShouldAutoSyncModifiersStates() && m.SyncStates())
                    serializedObject.Update();

                var targetStates = m.properties.Select(p => p.state).ToList();
                if (!m.manualStateEditing && targets.Length > 1)
                {
                    if (targetStates.Count != alphabetically.Count || !targetStates.ToHashSet().SetEquals(states))
                    {
                        EditorGUILayout.HelpBox("Can't multi-edit modifiers with different state lists.",
                            MessageType.Error);
                        return;
                    }

                    if (!targetStates.SequenceEqual(alphabetically))
                    {
                        m.properties = m.properties.OrderBy(x => x.state).ToList();
                        EditorUtility.SetDirty(target);
                    }
                }
                else
                {
                    states.UnionWith(targetStates);
                }
            }

            serializedObject.Update();
            
            propertiesUpdated = false;

            EditorGUI.BeginChangeCheck();
            ShowNode(states);
            if (EditorGUI.EndChangeCheck())
            {
                // node for modifier changed - update last change time
                BaseStateNodeEditor.lastChangeTime = Time.realtimeSinceStartup;
            }

            using var _ = ListPool<SerializedProperty>.Get(out var customProps);
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

                var icon = EditorGUIUtility.IconContent("d_RotateTool");
                icon.text = "  Sync component value(s)";
                if (targets.Length == 1 &&
                    modifier is ISupportValueFreeze valueFreeze && GUILayout.Button(icon))
                {
                    Undo.RecordObject(modifier, "Freeze value");
                    valueFreeze.FreezeValue();
                }
            }

            if (states.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("States", EditorStyles.whiteLargeLabel);
                propertiesUpdated |= ShowProperties();

                var icon = EditorGUIUtility.IconContent("d_RotateTool");
                icon.text = "  Sync all states value(s)";
                
                if (targets.Length == 1 && 
                    modifier is ISupportPropertyFreeze propFreeze && GUILayout.Button(icon)) 
                {
                    Undo.RecordObject(modifier, "Freeze properties");
                    foreach (var prop in modifier.properties) {
                        propFreeze.FreezeProperty(prop);
                    }
                }
            }

            if (targets.Length == 1 && modifier.manualStateEditing)
            {
                var icon = EditorGUIUtility.IconContent("d_Toolbar Plus");
                icon.text = "New State";
                if (GUILayout.Button(icon))
                {
                    modifier.AddState("New State");
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
                    EditorGUILayout.HelpBox($"Could not find parent node automatically, editor animations are disabled.",
                        MessageType.Warning);
                }
                else
                    if (GUILayout.Button($"Automatically selecting parent (<b><color=cyan>{modifier.GetNode().name}</color></b>)",
                        helpboxStyle))
                {
                    EditorGUIUtility.PingObject(modifier.GetNode());
                }

                if (modifier.manualStateEditing)
                {
                    EditorGUILayout.HelpBox($"Manual state editing is enabled, " +
                                            $"{StateFunction.kDefaultState} will be used if active state is not defined here.", MessageType.Warning);
                }
            }

            if (modifier.manualStateEditing)
                return;

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

        bool ShowProperties()
        {
            var updated = false;
            var properties = serializedObject.FindProperty(nameof(Modifier.properties));
            
            using var _ = ListPool<(string stateName, SerializedProperty prop, int index)>.Get(out var sortedStateProps);
            
            sortedStateProps.Clear();
            for (var i = 0; i < properties.arraySize; ++i)
            {
                var property = properties.GetArrayElementAtIndex(i);
                var propState = property.FindPropertyRelative(nameof(Modifier.PropertyBase.state)).stringValue;
                sortedStateProps.Add((propState, property, i));
            }

            // draw the editor for each value in property
            foreach (var (propState, property, i) in sortedStateProps)
            {
                DrawSeparator();

                // name 
                var suffix = "";
                if (Application.isPlaying)
                {
                    var stateId = Database.instance.GetStateID(propState);
                    if (stateId != StateFunction.emptyStateId)
                    {
                        if (modifier.GetActiveState() == stateId)
                        {
                            suffix = $" (current, {Mathf.RoundToInt(modifier.transitionProgress * 100)}%)";
                        }
                        else
                        {
                            suffix = $" ({Mathf.RoundToInt(modifier.GetTransitionProgress(stateId) * 100)}%)";
                        }
                    }
                }
            
                var origColor = GUI.contentColor;
                var propertyBase = modifier.properties[i];
                void UtilityButtons(bool canEdit)
                {
                    if (targets.Length > 1)
                        return;

                    GUI.enabled = canEdit;
                    if (modifier is ISupportPropertyFreeze propFreeze
                        && GUILayout.Button(EditorGUIUtility.IconContent("RotateTool On", "Freeze"), GUILayout.Width(25)))
                    {
                        Undo.RecordObject(modifier, "Freeze value");
                        propFreeze.FreezeProperty(propertyBase);
                    }
                    GUI.enabled = true;

                    if (Application.isPlaying)
                        return;

                    GUI.contentColor = coro != null ? Color.green : origColor;
                    GUI.enabled = modifier.animatableInEditor;
                    if (GUILayout.Button(EditorGUIUtility.IconContent("d_PlayButton"),
                            GUILayout.Width(25)))
                    {
                        void Animate(float speed)
                        {
                            if (coro != null)
                                EditorCoroutineUtility.StopCoroutine(coro);

                            var sourceState = !string.IsNullOrEmpty(lastAnimatedState)
                                ? lastAnimatedState 
                                : modifier.properties.OrderByDescending(p => p.state != propertyBase.state).First().state;
                            lastAnimatedState = propertyBase.state;
                            coro = EditorCoroutineUtility.StartCoroutine(EditorTransitions.TransitionAsync(new[] { modifier }, 
                                    sourceState, propertyBase.state,
                                    speed, () => coro = null), this);
                        }

                        if (Event.current.button == 1)
                        {
                            // right click
                            var menu = new GenericMenu();
                            foreach (var speed in new[] { .1f, .25f, .5f, 1f, 1.25f, 1.5f, 2f })
                            {
                                menu.AddItem(new GUIContent($"x{speed}"), false, () => Animate(speed));
                            }

                            menu.ShowAsContext();
                        }
                        else
                        {
                            Animate(1f);
                        }
                    }

                    GUI.contentColor = origColor;
                    GUI.enabled = true;
                }

                void Menu()
                {
                    var menu = new GenericMenu();
                    DexteritySettingsProvider.settings.BuildCache();
                    
                    if (!string.IsNullOrEmpty(propertyBase.savedPropertyKey))
                    {
                        menu.AddDisabledItem(new GUIContent($"Bind (Global): {propertyBase.savedPropertyKey}"));
                        menu.AddSeparator("");
                    }
                    else if (!string.IsNullOrEmpty(propertyBase.localStateReference))
                    {
                        menu.AddDisabledItem(new GUIContent($"Bind (Local): {propertyBase.localStateReference}"));
                        menu.AddSeparator("");
                    }
                    
                    foreach (var savedProp in DexteritySettingsProvider.settings.GetSavedPropertiesForType(
                                 propertyBase.GetType()))
                    {
                        menu.AddItem(new GUIContent($"Bind To/Global/{savedProp}"), false, () =>
                        {
                            Undo.RecordObject(modifier, "Bind property");
                            propertyBase.savedPropertyKey = savedProp;
                            EditorUtility.SetDirty(modifier);
                        });
                    }
                    foreach (var prop in modifier.properties.Where(p => p != propertyBase 
                                                                        && string.IsNullOrEmpty(p.localStateReference)
                                                                        && string.IsNullOrEmpty(p.savedPropertyKey)))
                    {
                        menu.AddItem(new GUIContent($"Bind To/{prop.state}"), false, () =>
                        {
                            Undo.RecordObject(modifier, "Bind property");
                            propertyBase.localStateReference = prop.state;
                            EditorUtility.SetDirty(modifier);
                        });
                    }
                    if (!string.IsNullOrEmpty(propertyBase.savedPropertyKey) || !string.IsNullOrEmpty(propertyBase.localStateReference))
                    {
                        menu.AddItem(new GUIContent("Unbind"), false, () =>
                        {
                            Undo.RecordObject(modifier, "Unbind property");
                            propertyBase.savedPropertyKey = "";
                            propertyBase.localStateReference = "";
                            EditorUtility.SetDirty(modifier);
                        });
                    }

                    menu.AddItem(new GUIContent("Save As..."), false, () =>
                    {
                        SaveProperty(modifier, propertyBase.state);
                    });
                    
                    menu.AddSeparator("");
                    
                    menu.AddItem(new GUIContent("Open Settings (Popup)"), false, () =>
                    {
                        var settings = DexteritySettingsProvider.settings;
                        PopUpAssetInspector.Create(settings);
                    });

                    if (targets.Length == 1 && modifier.manualStateEditing)
                    {
                        menu.AddSeparator("");
                        menu.AddItem(new GUIContent("Delete"), false, () =>
                        {
                            Undo.RecordObject(modifier, "Delete property");
                            modifier.properties.RemoveAt(i);
                            EditorUtility.SetDirty(modifier);
                        });
                    }
                    
                    menu.ShowAsContext();
                }

                updated = ShowSingleStateFields(modifier, property, propertyBase.GetType(),
                    propertyBase.state, UtilityButtons, Menu, suffix, 
                    targets.Length == 1 && modifier.manualStateEditing);
            }
            DrawSeparator();

            if (Application.isPlaying) // debug view
                Repaint();

            return updated;
        }

        public static bool ShowSingleStateFields(Modifier modifier, 
            SerializedProperty serializedProperty,
            Type propertyType,
            string propertyKey,
            Action<bool> utilityUiFunction = null,
            Action menuFunction = null, string suffix = "", bool manualStateEditing = false)
        {
            var updated = false;
            var origColor = GUI.contentColor;

            var savedProperty = serializedProperty.FindPropertyRelative(nameof(Modifier.PropertyBase.savedPropertyKey));
            var localStateProperty = serializedProperty.FindPropertyRelative(nameof(Modifier.PropertyBase.localStateReference));
            var savedPropKey = string.Empty;
            var localStateKey = string.Empty;
            if (!string.IsNullOrEmpty(savedProperty.stringValue))
            {
                DexteritySettingsProvider.settings.BuildCache();
                var savedProp =
                    DexteritySettingsProvider.settings.GetSavedProperty(propertyType, savedProperty.stringValue);
                if (savedProp == null)
                {
                    Debug.LogError($"Saved property {savedPropKey} not found for {propertyType}, clearing");
                    savedProperty.stringValue = "";
                }
                else
                {
                    savedPropKey = savedProperty.stringValue;
                
                    var index = DexteritySettingsProvider.settings.namedProperties
                        .FindIndex(p => p.name == savedPropKey && p.property.GetType() == propertyType);

                    if (index == -1)
                        Debug.LogError($"Saved property {savedPropKey} not found for {propertyType.Name}");
                    else
                    {
                        var serializedObject = new SerializedObject(DexteritySettingsProvider.settings);
                        serializedProperty = serializedObject.FindProperty(nameof(DexteritySettings.namedProperties))
                            .GetArrayElementAtIndex(index)
                            .FindPropertyRelative(nameof(DexteritySettings.SavedProperty.property));
                    }
                }
            }
            else if (!string.IsNullOrEmpty(localStateProperty.stringValue) && modifier != null)
            {
                var prop = modifier.properties.FirstOrDefault(p => p.state == localStateProperty.stringValue);
                if (prop == null)
                {
                    Debug.LogError($"Local state property {localStateProperty.stringValue} not found for {propertyType}, clearing");
                    localStateProperty.stringValue = "";
                }
                else
                {
                    var modifierObject = new SerializedObject(modifier);
                    serializedProperty = modifierObject.FindProperty(nameof(Modifier.properties))
                        .GetArrayElementAtIndex(modifier.properties.IndexOf(prop));
                    localStateKey = localStateProperty.stringValue;
                }
            }

            using var _ = ListPool<SerializedProperty>.Get(out var stateProps);
            // fields
            foreach (var field in Utils.GetChildren(serializedProperty))
            {
                switch (field.name)
                {
                    case nameof(Modifier.PropertyBase.savedPropertyKey):
                    case nameof(Modifier.PropertyBase.localStateReference):
                    case nameof(Modifier.PropertyBase.state) when !manualStateEditing:
                        continue;
                    default:
                        stateProps.Add(field.Copy());
                        break;
                }
            }


            void ShowMenuIfRightClick()
            {
                if (Event.current != null 
                    && Event.current.type == EventType.MouseDown
                    && Event.current.button == 1 
                    && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                {
                    menuFunction?.Invoke();
                    Event.current.Use();
                }
            }

            var isSaved = !string.IsNullOrEmpty(savedPropKey) || !string.IsNullOrEmpty(localStateKey);
            var reference = !string.IsNullOrEmpty(savedPropKey) ? savedPropKey : localStateKey;
            if (stateProps.Count > 1)
            {
                // multiple - fold
                EditorGUILayout.BeginHorizontal();
                var isOpen = true;
                if (foldedStates.TryGetValue(propertyKey, out var state))
                    isOpen = state;
                foldedStates[propertyKey] = EditorGUILayout.Foldout(isOpen,
                    $"{propertyKey}{suffix}", true, EditorStyles.foldoutHeader);
                ShowMenuIfRightClick();
                GUI.contentColor = origColor;

                if (isSaved)
                {
                    GUI.contentColor = Color.yellow;
                    GUILayout.Space(10);
                    EditorGUILayout.LabelField($"[{reference}]", GUILayout.MinWidth(50));
                    GUI.contentColor = origColor;
                }

                utilityUiFunction?.Invoke(!isSaved);

                EditorGUILayout.EndHorizontal();

                // show fields
                if (!foldedStates[propertyKey]) 
                    return false;
                
                if (isSaved)
                    GUI.enabled = false;
                foreach (var field in stateProps)
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(field);
                    updated |= EditorGUI.EndChangeCheck();
                }

                GUI.enabled = true;
            }
            else if (stateProps.Count == 1)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{propertyKey}{suffix}", GUILayout.MinWidth(30));
                ShowMenuIfRightClick();
                GUI.contentColor = origColor;
                
                if (isSaved)
                {
                    GUI.contentColor = Color.yellow;
                    GUILayout.Space(10);
                    EditorGUILayout.LabelField($"[{reference}]", GUILayout.MinWidth(50));
                    GUI.contentColor = origColor;
                    GUI.enabled = false;
                }

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(stateProps[0], GUIContent.none, GUILayout.MinWidth(30));
                updated |= EditorGUI.EndChangeCheck();
                
                GUI.enabled = true;

                utilityUiFunction?.Invoke(!isSaved);

                EditorGUILayout.EndHorizontal();
            }

            return updated;
        }

        private static void SaveProperty(Modifier modifier, string state)
        {
            var prop = modifier.properties.FirstOrDefault(p => p.state == state);
            if (prop == null)
                return;
            
            var key = EditorInputStringDialog.Show("Save Property", "Name this binding", 
                $"{modifier.name}.{state}");

            var settings = DexteritySettingsProvider.settings;
            settings.SavePropertyAs(prop, key);
            EditorUtility.SetDirty(settings);
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

        public class PopUpAssetInspector : EditorWindow
        {
            private Object asset;
            private Editor assetEditor;
            private Vector2 scrollPos;

            public static ModifierEditor.PopUpAssetInspector Create(Object asset)
            {
                var window = CreateWindow<ModifierEditor.PopUpAssetInspector>($"{asset.name} | {asset.GetType().Name}");
                window.asset = asset;
                window.assetEditor = CreateEditor(asset);
                return window;
            }

            private void OnGUI()
            {
                GUI.enabled = false;
                asset = EditorGUILayout.ObjectField("Asset", asset, asset.GetType(), false);
                GUI.enabled = true;

                // make scrollable inspector
                scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
                assetEditor.OnInspectorGUI();
                EditorGUILayout.EndScrollView();
            }
        }
    }
}
