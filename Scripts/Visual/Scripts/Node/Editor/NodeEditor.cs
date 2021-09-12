using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System;

namespace OneHamsa.Dexterity.Visual
{

    [CustomEditor(typeof(Node))]
    public class NodeEditor : Editor
    {
        Node node;

        public override void OnInspectorGUI()
        {
            node = target as Node;

            serializedObject.Update();

            ShowChooseReference();
            ShowChooseInitialState();
            ShowOverrides();
            ShowDebug();
            serializedObject.ApplyModifiedProperties();
        }

        private void ShowChooseInitialState()
        {
            if (node.reference == null)
                return;

            var prop = serializedObject.FindProperty(nameof(Node.initialState));

            var states = node.reference.stateFunction.GetStates().ToList();

            EditorGUI.BeginChangeCheck();
            var newIdx = EditorGUILayout.Popup("Initial state", states.IndexOf(node.initialState), states.ToArray());
            if (EditorGUI.EndChangeCheck())
            {
                prop.stringValue = states[newIdx];
            }
        }

        void ShowChooseReference()
        {
            var prop = serializedObject.FindProperty(nameof(Node.reference));

            var references = FindAssetsByType<NodeReference>();
            var names = references.Select(r => r.name);
            var currentIdx = references.IndexOf(node.reference);
            EditorGUI.BeginChangeCheck();
            var newIdx = EditorGUILayout.Popup("Reference", currentIdx, names.ToArray());
            if (EditorGUI.EndChangeCheck())
            {
                EditorGUIUtility.PingObject(node.reference);
                prop.objectReferenceValue = references[newIdx];
            }
            

            GUI.enabled = node.reference != null;
            if (GUILayout.Button("Select Reference Asset"))
            {
                Selection.activeObject = node.reference;
            }
            GUI.enabled = true;
        }

        void ShowOverrides()
        {
            var overridesProp = serializedObject.FindProperty(nameof(Node.overrides));
            EditorGUILayout.PropertyField(overridesProp);
        }

        static bool debugOpen;
        void ShowDebug()
        {
            debugOpen = EditorGUILayout.Foldout(debugOpen, "Debug", true, EditorStyles.foldoutHeader);
            if (!debugOpen)
                return;

            var outputFields = node.outputFields;
            var overrides = node.cachedOverrides;
            var unusedOverrides = new HashSet<Node.OutputOverride>(overrides.Values);
            var origColor = GUI.color;
            var overridesStr = overrides.Count == 0 ? "" : $", {overrides.Count} overrides";

            {                
                GUI.color = outputFields.Count == 0 ? Color.yellow : Color.grey;
                EditorGUILayout.LabelField($"{outputFields.Count} output fields{overridesStr}", EditorStyles.helpBox);
                GUI.color = origColor;
            }

            foreach (var field in outputFields.Values.ToArray().OrderBy(f => f.GetValue() == Node.emptyFieldValue))
            {
                var value = field.GetValueWithoutOverride();
                string strValue = value.ToString();
                if (value == Node.emptyFieldValue)
                {
                    GUI.color = Color.gray;
                    strValue = "(empty)";
                }
                if (overrides.ContainsKey(field.definitionId))
                {
                    var outputOverride = overrides[field.definitionId];
                    GUI.color = Color.magenta;
                    strValue = $"{outputOverride.value} ({StrikeThrough(strValue)})";
                    unusedOverrides.Remove(outputOverride);
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(Manager.instance.GetFieldDefinition(field.definitionId).name);
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(strValue);
                EditorGUILayout.EndHorizontal();

                GUI.color = origColor;
            }

            foreach (var outputOverride in unusedOverrides)
            {
                GUI.color = Color.magenta;
                    
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(outputOverride.outputFieldName);
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(outputOverride.value.ToString());
                EditorGUILayout.EndHorizontal();

                GUI.color = origColor;
            }

            Repaint();
        }

        static List<T> FindAssetsByType<T>() where T : UnityEngine.Object
        {
            List<T> assets = new List<T>();
            string[] guids = AssetDatabase.FindAssets(string.Format("t:{0}", typeof(T)));
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (asset != null)
                {
                    assets.Add(asset as T);
                }
            }
            return assets;
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

        static void DrawSeparator(Color color)
        {
            EditorGUILayout.Space();
            var rect = EditorGUILayout.BeginHorizontal();
            Handles.color = color;
            Handles.DrawLine(new Vector2(rect.x, rect.y), new Vector2(rect.width + 15, rect.y));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }
    }
}