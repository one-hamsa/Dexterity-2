using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace OneHamsa.Dexterity.Visual
{
    [CustomEditor(typeof(StateFunction))]
    public class StateFunctionEditor : Editor
    {
        bool showStates = true, showFields = true;

        public override void OnInspectorGUI()
        {
            ShowInspectorGUI(true);
        }

        public void ShowInspectorGUI(bool showEditorButton)
        {
            var origColor = GUI.color;
            var sf = target as StateFunction;

            var states = sf.GetStates().Where(x => !string.IsNullOrEmpty(x)).ToArray();
            if (showStates = EditorGUILayout.BeginFoldoutHeaderGroup(showStates, $"Used States ({states.Length})"))
            {
                foreach (var state in states)
                {
                    if (string.IsNullOrEmpty(state))
                        continue;
                    GUILayout.Label($"- {state}");
                }
                if (states.Length == 0)
                {
                    EditorGUILayout.HelpBox("No states defined", MessageType.None);
                }

            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            var fields = sf.GetFields().Where(x => !string.IsNullOrEmpty(x)).ToArray();
            if (showFields = EditorGUILayout.BeginFoldoutHeaderGroup(showFields, $"Used Fields ({fields.Length})"))
            {
                foreach (var field in fields)
                {
                    if (string.IsNullOrEmpty(field))
                        continue;
                    GUILayout.Label($"- {field}");
                }
                if (fields.Length == 0)
                {
                    EditorGUILayout.HelpBox("No fields defined", MessageType.None);
                }

            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            var validated = false;
            string error;
            try
            {
                validated = sf.Validate();
                error = sf.errorString;
            }
            catch (Exception e)
            {
                error = $"({e.GetType()}) {e.Message}";
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Validated?", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (validated)
            {
                GUI.color = Color.green;
                GUILayout.Label("Yes", EditorStyles.boldLabel);
            }
            else
            {
                GUI.color = Color.red;
                GUILayout.Label("No", EditorStyles.boldLabel);
            }
            GUI.color = origColor;
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(error))
                EditorGUILayout.HelpBox(error, MessageType.Error);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Registered?", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            var contains = Manager.Instance.stateFunctions.Contains(sf);
            if (contains)
            {
                GUI.color = Color.green;
                GUILayout.Label("Yes", EditorStyles.boldLabel);
            }
            else
            {
                GUI.color = Color.yellow;
                GUILayout.Label("No", EditorStyles.boldLabel);
            }
            GUI.color = origColor;
            GUILayout.EndHorizontal();

            if (!contains)
            {
                EditorGUILayout.HelpBox("This function won't run unless registered.", MessageType.Warning);
                if (GUILayout.Button("Register"))
                {
                    Manager.Instance.stateFunctions.Add(sf);
                    EditorUtility.SetDirty(Manager.Instance);
                    EditorSceneManager.MarkSceneDirty(Manager.Instance.gameObject.scene);
                }
            }
            else
            {
                if (GUILayout.Button("Unregister"))
                {
                    Manager.Instance.stateFunctions.Remove(sf);
                    EditorUtility.SetDirty(Manager.Instance);
                    EditorSceneManager.MarkSceneDirty(Manager.Instance.gameObject.scene);
                }
            }

            if (showEditorButton)
            {
                GUILayout.Space(100);
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Open Editor", GUILayout.Width(120), GUILayout.Height(55)))
                {
                    StateFunctionGraph.CreateGraphViewWindow(sf);
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
        }
    }
}