﻿using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System;
using Unity.EditorCoroutines.Editor;

namespace OneHamsa.Dexterity
{
    public abstract class BaseStateNodeEditor : Editor
    {
        static bool modifiersDebugOpen;
        private static int speedIndex = -1;
        
        BaseStateNode baseNode;
        private HashSet<string> states = new();
        private List<string> previewStates = new();
        private List<string> previewStateNames = new();
        private EditorCoroutine coro;

        private int previewStateIndex;
        private HashSet<Modifier> modifiers;
        private bool modifiersCacheInvalidated 
            => modifiers == null || (lastModifiersUpdateTime < lastChangeTime 
                                     // don't invalidate cache if editor transition is in place
                                     && coro == null);
        private double lastModifiersUpdateTime;
        public static double lastChangeTime;

        static BaseStateNodeEditor()
        {
            EditorApplication.hierarchyChanged += () => lastChangeTime = EditorApplication.timeSinceStartup;
        }
        
        protected virtual void Legacy_OnInspectorGUI()
        {
            states.Clear();
            var first = true;
            foreach (var t in targets) {
                foreach (var state in (t as BaseStateNode).GetStateNames()) {
                    if (states.Add(state) && !first) {
                        EditorGUILayout.HelpBox("Can't multi-edit nodes with different state lists.", MessageType.Error);
                        return;
                    }
                }
                first = false;
            }

            baseNode = target as BaseStateNode;

            serializedObject.Update();

            EditorGUILayout.LabelField("Fields & State", EditorStyles.whiteLargeLabel);

            ShowChooseInitialState();

            ShowFields();

            ShowDelays();

            ShowOverrides();

            EditorGUILayout.Space();
            var origColor = GUI.contentColor;
            GUI.contentColor = Color.yellow;
            EditorGUILayout.LabelField("Debug", EditorStyles.whiteLargeLabel);
            GUI.contentColor = origColor;
            
            if (targets.Length <= 1)
                ShowSingleTargetDebug();
            ShowAllTargetsDebug();

            ShowWarnings();
            serializedObject.ApplyModifiedProperties();
        }

        protected abstract void ShowFields();

        private void ShowChooseInitialState()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(BaseStateNode.initialState)));
        }

        void ShowOverrides()
        {
            ShowFieldOverrides();
            ShowStateOverride();
        }

        protected virtual void ShowFieldOverrides()
        {
            
        }

        private void ShowStateOverride()
        {
            if (targets.Length > 1)
                return;

            GUI.enabled = Application.IsPlaying(this);
            var overrideStateProp = serializedObject.FindProperty(nameof(BaseStateNode.overrideState));

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(overrideStateProp, new GUIContent("State Override"));
            if (EditorGUI.EndChangeCheck())
            {
                if (string.IsNullOrEmpty(overrideStateProp.stringValue))
                    baseNode.ClearStateOverride();
                else
                    baseNode.SetStateOverride(Database.instance.GetStateID(overrideStateProp.stringValue));
            }
            GUI.enabled = true;
        }

        void ShowSingleTargetDebug()
        {
            if (!Application.IsPlaying(this))
            {
                ShowPreviewState();
            }
            
            ShowModifiers();
                
            if (Application.IsPlaying(this))
            {
                ShowActiveState();
                ShowFieldValues();
            }
        }

        private void ShowModifiers()
        {
            if (!baseNode.enabled)
                return;
            
            var _modifiers = GetModifiers();
            if (_modifiers == null)
                return;
            var modifiers = _modifiers.ToList();

            if (baseNode.ShouldAutoSyncModifiersStates())
            {
                // make sure all are up to date
                foreach (var modifier in modifiers)
                    modifier.SyncStates();
            }
            else
            {
                EditorGUILayout.HelpBox($"Auto-Sync for modifiers states is disabled, " +
                                        $"states might not be synced", MessageType.Warning);
                
                if (GUILayout.Button("Sync Now"))
                {
                    foreach (var modifier in modifiers)
                        modifier.SyncStates();
                }
            }

            if (!(modifiersDebugOpen = EditorGUILayout.Foldout(modifiersDebugOpen, $"Modifiers ({modifiers.Count()})", true, EditorStyles.foldoutHeader)))
                return;
            
            if (GUILayout.Button("Save Current As..."))
            {
                // show menu to select which state to save
                var menu = new GenericMenu();
                foreach (var state in states)
                {
                    menu.AddItem(new GUIContent(state), false, () => SaveModifiers(state));
                }
                menu.ShowAsContext();
                
                void SaveModifiers(string state)
                {
                    foreach (var modifier in modifiers)
                    {
                        try
                        {
                            if (modifier is ISupportPropertyFreeze freeze)
                            {
                                modifier.SyncStates();
                                var activeProp = modifier.properties.First(p => p.state == state);
                                freeze.FreezeProperty(activeProp);
                            }
                            else
                            {
                                Debug.LogWarning(
                                    $"Modifier {modifier.name} does not support property freeze, skipping.", modifier);
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"Failed to save modifier {modifier.name} for state {state}",
                                modifier);
                            Debug.LogException(e, modifier);
                        }
                    }
                }
            }

            var origColor = GUI.contentColor;
            foreach (var m in modifiers)
            {
                GUI.contentColor = m.isActiveAndEnabled ? origColor : Color.gray;
                EditorGUILayout.BeginHorizontal();
                var icon = EditorGUIUtility.ObjectContent(m, m.GetType());
                EditorGUILayout.LabelField(icon);
                if (GUILayout.Button(EditorGUIUtility.IconContent("d_Prefab Icon"), GUILayout.MaxWidth(30), GUILayout.MaxHeight(20)))
                {
                    Selection.activeObject = m;
                }
                EditorGUILayout.EndHorizontal();
                GUI.contentColor = new Color(.75f, .75f, .75f);
                EditorGUILayout.LabelField(GetPath(m.gameObject), EditorStyles.miniLabel);
                GUI.contentColor = origColor;
            }
        }

        private void ShowDelays()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(BaseStateNode.delays)));
        }

        protected virtual void ShowFieldValues()
        {
        }

        private void ShowActiveState()
        {
            var origColor = GUI.color;
            
            if (baseNode.GetActiveState() != -1)
            {
                var style = new GUIStyle(EditorStyles.helpBox);
                style.alignment = TextAnchor.MiddleCenter;
                style.fontSize = 14;

                GUI.color = Color.green;
                GUILayout.Label(Database.instance.GetStateAsString(baseNode.GetActiveState()), style);
                GUI.color = origColor;
            }
        }

        protected virtual void ShowAllTargetsDebug()
        {
        }

        private void ShowPreviewState()
        {
            previewStates.Clear();
            previewStateNames.Clear();

            previewStates.Add(null);
            previewStateNames.Add("(None)");

            foreach (var state in states) {
                previewStates.Add(state);
                previewStateNames.Add(state);
            }

            var origColor = GUI.contentColor;
            var origBgColor = GUI.backgroundColor;
            GUI.contentColor = new Color(1f, 1f, .75f);
            GUI.backgroundColor = new Color(1f, 1f, .75f);

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            GUILayout.Label("Preview");
            var sourceState = previewStateIndex == 0 ? baseNode.initialState : previewStates[previewStateIndex];
            var newIndex = EditorGUILayout.Popup("", previewStateIndex, previewStateNames.ToArray(),
                GUILayout.MaxWidth(150));
            if (newIndex != 0)
                previewStateIndex = newIndex;

            var didChange = EditorGUI.EndChangeCheck();

            GUI.backgroundColor = origBgColor;
            GUI.contentColor = coro != null ? Color.green : origColor;

            var speeds = new [] { 0.1f, 0.25f, 0.5f, 1f, 1.25f, 1.5f, 2f };
            var speedsNames = speeds.Select(s => $"x{s}").ToArray();
            if (speedIndex == -1)
                speedIndex = Array.IndexOf(speeds, 1f);
            speedIndex = EditorGUILayout.Popup("", speedIndex, speedsNames, GUILayout.Width(50));

            GUI.contentColor = origColor;

            if (didChange && previewStates[previewStateIndex] != null)
            {
                if (coro != null)
                    EditorCoroutineUtility.StopCoroutine(coro);

                // collect all children modifiers
                var allModifiers = GetModifiers();

                if (allModifiers == null)
                    return;
                
                var modifiers = new HashSet<Modifier>();
                var skippedModifiersPaths = "";
                var skippedModifiersCount = 0;
                foreach (var modifier in allModifiers)
                {
                    if (!modifier.animatableInEditor)
                    {
                        if (modifier.isActiveAndEnabled)
                        {
                            skippedModifiersPaths += $"{GetPath(modifier.gameObject)} ({modifier.GetType().Name})\n";
                            skippedModifiersCount++;
                        }

                        continue;
                    }
                    
                    modifiers.Add(modifier);   
                }
                // if (skippedModifiersCount > 0)
                //     Debug.Log($"Editor Preview: not animating {skippedModifiersCount} modifiers\n{skippedModifiersPaths}");

                coro = EditorCoroutineUtility.StartCoroutine(
                    EditorTransitions.TransitionAsync(modifiers, 
                        sourceState, previewStates[previewStateIndex], 
                        speeds[speedIndex], () => coro = null), this);
            }
            EditorGUILayout.EndHorizontal();
        }

        protected virtual void ShowWarnings()
        {
            if (!states.Contains(baseNode.initialState))
            {
                EditorGUILayout.HelpBox($"Initial State should be selected", MessageType.Warning);
            }
            if (targets.Length > 1) 
            {
                EditorGUILayout.HelpBox($"Some options are hidden in multi-edit mode", MessageType.Warning);
            }
        }
        
        private IEnumerable<Modifier> GetModifiers()
        {
            if (Application.IsPlaying(this))
                return baseNode.GetModifiers();

            if (!modifiersCacheInvalidated)
                return modifiers;
            
            modifiers = GetModifiers(baseNode).ToHashSet();
            lastModifiersUpdateTime = EditorApplication.timeSinceStartup;
            return modifiers;
        }
        
        private static IEnumerable<Modifier> GetModifiers(BaseStateNode baseNode)
        {
            if (Application.IsPlaying(baseNode))
                return baseNode.GetModifiers();

            var modifiers = new HashSet<Modifier>();
            
            // see https://forum.unity.com/threads/findobjectsoftype-is-broken-when-invoked-from-inside-prefabstage-nested-prefabs.684037/
            foreach (var modifier in Resources.FindObjectsOfTypeAll<Modifier>()) {
                if (modifier.GetNode() == baseNode 
                    // don't collect hidden modifiers - these are used for non-trivial editor animations
                    && modifier.gameObject.hideFlags == HideFlags.None) 
                    modifiers.Add(modifier);
            }

            return modifiers;
        }
        
        private static string GetPath(GameObject go)
        {
            string name = go.name;
            while (go.transform.parent != null)
            {

                go = go.transform.parent.gameObject;
                name = go.name + "/" + name;
            }
            return name;
        }

        public static string StrikeThrough(string s)
        {
            string strikethrough = "";
            foreach (char c in s)
            {
                strikethrough = strikethrough + c + '\u0336';
            }
            return strikethrough;
        }

        protected static void DrawSeparator(Color color)
        {
            EditorGUILayout.Space();
            var rect = EditorGUILayout.BeginHorizontal();
            Handles.color = color;
            Handles.DrawLine(new Vector2(rect.x, rect.y), new Vector2(rect.width + 15, rect.y));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }
        
        [MenuItem("CONTEXT/BaseStateNode/Convert to Simple Enum Node")]
        private static void ConvertToSimpleEnumNode(MenuCommand command)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Convert to Simple Enum Node");
            var group = Undo.GetCurrentGroup();
            
            var baseNode = (BaseStateNode)command.context;
            var simpleEnumNode = Undo.AddComponent<SimpleEnumNode>(baseNode.gameObject);
            simpleEnumNode.manualStates.AddRange(baseNode.GetStateNames());
            simpleEnumNode.initialState = baseNode.initialState;
            simpleEnumNode.delays = baseNode.delays;
            simpleEnumNode.overrideState = baseNode.overrideState;
            simpleEnumNode.SetAutoSyncModifiersStates(baseNode.ShouldAutoSyncModifiersStates());

            foreach (var modifier in GetModifiers(baseNode))
            {
                Undo.RecordObject(modifier, "Convert to Simple Enum Node");
                modifier._node = simpleEnumNode;
                EditorUtility.SetDirty(modifier);
            }
            
            Undo.RecordObject(baseNode, "Convert to Simple Enum Node");
            baseNode.enabled = false;
            EditorUtility.SetDirty(baseNode);

            simpleEnumNode.Cache_Editor();
            EditorUtility.SetDirty(simpleEnumNode);
            
            Undo.CollapseUndoOperations(group);
        }

        [MenuItem("CONTEXT/BaseStateNode/Convert to Binding Enum Node")]
        private static void ConvertToBindingEnumNode(MenuCommand command)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Convert to Binding Enum Node");
            var group = Undo.GetCurrentGroup();
            
            var baseNode = (BaseStateNode)command.context;
            var bindingEnumNode = Undo.AddComponent<BindingEnumNode>(baseNode.gameObject);
            bindingEnumNode.initialState = baseNode.initialState;
            bindingEnumNode.delays = baseNode.delays;
            bindingEnumNode.overrideState = baseNode.overrideState;
            bindingEnumNode.SetAutoSyncModifiersStates(false);

            foreach (var modifier in GetModifiers(baseNode))
            {
                Undo.RecordObject(modifier, "Convert to Binding Enum Node");
                modifier._node = bindingEnumNode;
                EditorUtility.SetDirty(modifier);
            }
            
            Undo.RecordObject(baseNode, "Convert to Binding Enum Node");
            baseNode.enabled = false;
            EditorUtility.SetDirty(baseNode);

            EditorUtility.SetDirty(bindingEnumNode);
            
            Undo.CollapseUndoOperations(group);
        }
        
        [MenuItem("CONTEXT/BaseStateNode/Rename State...")]
        private static void RenameState(MenuCommand command)
        {
            var baseNode = (BaseStateNode)command.context;
            RenameStateDialog.Create(baseNode);
        }

        public class RenameStateDialog : EditorWindow
        {
            private BaseStateNode baseNode;
            private string stateName;
            private string error;
            private string newStateName;

            public static RenameStateDialog Create(BaseStateNode baseNode)
            {
                var dialog = GetWindow<RenameStateDialog>();
                dialog.baseNode = baseNode;
                dialog.stateName = baseNode.initialState;
                dialog.error = null;
                dialog.titleContent = new GUIContent("Rename State");
                dialog.Show();
                return dialog;
            }

            private void OnGUI()
            {
                EditorGUILayout.LabelField("Rename State", EditorStyles.whiteLargeLabel);
                EditorGUILayout.Space();
                var states = baseNode.GetStateNames().ToList();
                stateName = states[EditorGUILayout.Popup("State", states.IndexOf(stateName), states.ToArray())];
                newStateName = EditorGUILayout.TextField("New Name", newStateName);
                if (error != null)
                    EditorGUILayout.HelpBox(error, MessageType.Error);
                EditorGUILayout.Space();
                if (GUILayout.Button("Rename"))
                {
                    if (string.IsNullOrEmpty(stateName))
                    {
                        error = "State name can't be empty";
                        return;
                    }
                    
                    if (string.IsNullOrEmpty(newStateName))
                    {
                        error = "New state name can't be empty";
                        return;
                    }

                    // group undo
                    Undo.IncrementCurrentGroup();
                    var group = Undo.GetCurrentGroup();
                    
                    baseNode.RenameState(stateName, newStateName);
                    RenameModifierStates();
                    
                    EditorUtility.SetDirty(baseNode);

                    Undo.CollapseUndoOperations(group);
                    
                    Close();
                }
            }

            private void RenameModifierStates()
            {
                foreach (var modifier in GetModifiers(baseNode))
                {
                    modifier.RenameState(stateName, newStateName);
                    EditorUtility.SetDirty(modifier);
                }
            }
        }
    }
}
