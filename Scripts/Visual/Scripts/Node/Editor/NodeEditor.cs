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

        private void OnEnable()
        {
            debugOpen = Application.isPlaying;
        }

        public override void OnInspectorGUI()
        {
            node = target as Node;

            serializedObject.Update();

            ShowChooseReference();
            ShowChooseInitialState();
            var gatesUpdated = NodeReferenceEditor.ShowGates(serializedObject.FindProperty(nameof(Node.customGates)),
                node);
            ShowOverrides();
            ShowDebug();
            ShowWarnings();
            serializedObject.ApplyModifiedProperties();

            // do this after ApplyModifiedProperties() to ensure integrity
            if (gatesUpdated)
                node.NotifyGatesUpdate();
        }

        private void ShowChooseInitialState()
        {
            if (node.referenceAsset == null)
                return;

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(Node.initialState)));
        }

        void ShowChooseReference()
        {
            var prop = serializedObject.FindProperty(nameof(Node.referenceAsset));

            var references = Utils.FindAssetsByType<NodeReference>();
            var names = references.Select(r => r.name);
            var currentIdx = references.IndexOf(prop.objectReferenceValue as NodeReference);

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            var newIdx = EditorGUILayout.Popup("Reference", currentIdx, names.Append("New...").ToArray());
            if (EditorGUI.EndChangeCheck())
            {
                if (newIdx == names.Count())
                {
                    // selected new
                    CreateNewAsset();
                }
                else
                {
                    EditorGUIUtility.PingObject(references[newIdx]);
                    prop.objectReferenceValue = references[newIdx];
                }
            }
            

            GUI.enabled = node.referenceAsset != null;
            if (GUILayout.Button(EditorGUIUtility.IconContent("Animation.FilterBySelection"),
                EditorStyles.miniButton, GUILayout.Width(24)))
            {
                Selection.activeObject = node.referenceAsset;
            }

            // runtime
            GUI.enabled = node.reference != null;
            if (GUILayout.Button(EditorGUIUtility.IconContent("ScaleTool"),
                EditorStyles.miniButton, GUILayout.Width(24)))
            {
                NodeReferenceEditorWindow.Open(node.reference); 
            }
            EditorGUILayout.EndHorizontal();
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
            if (!Application.isPlaying)
            {
                return;
            }
            var origColor = GUI.color;

            // show state function button (play time)
            if (GUILayout.Button("State Function Live View"))
            {
                EditorWindow.GetWindow<StateFunctionGraphWindow>().InitializeGraph(node.reference.stateFunction);
            }

            if (node.activeState != -1)
            {
                var style = new GUIStyle(EditorStyles.helpBox);
                style.alignment = TextAnchor.MiddleCenter;
                style.fontSize = 14;

                GUI.color = Color.green;
                GUILayout.Label(Manager.instance.GetStateAsString(node.activeState), style);
                GUI.color = origColor;
            }

            debugOpen = EditorGUILayout.Foldout(debugOpen, "Debug", true, EditorStyles.foldoutHeader);
            if (!debugOpen)
                return;

            var outputFields = node.outputFields;
            var overrides = node.cachedOverrides;
            var unusedOverrides = new HashSet<Node.OutputOverride>(overrides.Values);
            var overridesStr = overrides.Count == 0 ? "" : $", {overrides.Count} overrides";
            {                
                EditorGUILayout.HelpBox($"{outputFields.Count} output fields{overridesStr}",
                    outputFields.Count == 0 ? MessageType.Warning : MessageType.Info);
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

        private void ShowWarnings()
        {
            if (node.referenceAsset == null)
            {
                EditorGUILayout.HelpBox("Must select Node Reference", MessageType.Error);
            }
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

        private void CreateNewAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject("Asset Path", $"Node Reference ({node.name})",
                "asset", "Choose new node reference asset location");
            if (string.IsNullOrEmpty(path))
                return;

            var asset = ScriptableObject.CreateInstance<NodeReference>();
            AssetDatabase.CreateAsset(asset, path.Substring(path.IndexOf("Assets/")));
            node.referenceAsset = asset;
            EditorUtility.SetDirty(node);
        }
    }
}