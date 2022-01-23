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
    [CustomEditor(typeof(Graph), true)]
    public class GraphEditor : Editor
    {
        private static bool debugFoldout;
        private static bool clustersFoldout;
        private static bool nodesFoldout;
        private static bool sortedFoldout;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (!Application.isPlaying)
                return;

            if (!(debugFoldout = EditorGUILayout.Foldout(debugFoldout, "Debug")))
                return;

            EditorGUI.indentLevel++;
            var graph = target as Graph;

            var startupTime = DateTime.Now.AddSeconds(-Time.realtimeSinceStartupAsDouble);
            var lastSuccessful = startupTime.AddSeconds(graph.lastSuccessfulUpdate);
            var lastAttempt = startupTime.AddSeconds(graph.lastUpdateAttempt);

            var origColor = GUI.contentColor;
            GUI.contentColor = graph.lastSortResult ? Color.green : Color.red;
            EditorGUILayout.LabelField("Last sort result", graph.lastSortResult ? "Success" : "Failure");
            GUI.contentColor = origColor;

            EditorGUILayout.LabelField("Last successful update",
                $"{lastSuccessful.ToLongTimeString()} ({(int)((DateTime.Now - lastSuccessful).TotalSeconds)}s ago)");
            EditorGUILayout.LabelField("Last update attempt",
                $"{lastAttempt.ToLongTimeString()} ({(int)((DateTime.Now - lastAttempt).TotalSeconds)}s ago)");
            EditorGUILayout.LabelField("Update Operations / Fields (#)", 
                $"{graph.updateOperations.ToString()} / {graph.updatedNodes.ToString()}");
            EditorGUILayout.LabelField("Update Frames (#)", graph.updateFrames.ToString());
            EditorGUILayout.LabelField("Max operations / frame", Graph.throttleOperationsPerFrame.ToString());

            EditorGUILayout.Space(20);
            ShowClustersAndNodes();
            ShowSortResult();

            EditorGUI.indentLevel--;
        }

        private void ShowSortResult()
        {
            var graph = target as Graph;

            if (!(sortedFoldout = EditorGUILayout.Foldout(sortedFoldout, $"{graph.sortedNodes.Count} Fields")))
                return;

            EditorGUILayout.HelpBox("Showing topologically-sorted list of fields.", MessageType.Info);

            EditorGUI.indentLevel++;

            foreach (var field in graph.sortedNodes)
            {
                ShowField(field);
            }

            EditorGUI.indentLevel--;
        }

        private void ShowClustersAndNodes()
        {
            var graph = target as Graph;

            // clusters
            var clusters = new Dictionary<int, List<BaseField>>();
            var nodes = new HashSet<Node>();
            foreach (var kv in graph.nodeToColor)
            {
                var actualColor = kv.Value;
                graph.colorToColorMap.TryGetValue(actualColor, out actualColor);
                if (!clusters.TryGetValue(actualColor, out var list))
                    clusters[actualColor] = list = new List<BaseField>();

                list.Add(kv.Key);
                if (kv.Key is Node.OutputField outputField)
                    nodes.Add(outputField.node);
            }

            ShowNodes(nodes);
            ShowClusters(clusters);
        }

        private static void ShowNodes(HashSet<Node> nodes)
        {
            if (!(nodesFoldout = EditorGUILayout.Foldout(nodesFoldout, $"{nodes.Count} Nodes")))
                return;

            EditorGUI.indentLevel++;

            foreach (var node in nodes)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(node.name);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Go"))
                    Selection.activeObject = node;
                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.indentLevel--;
        }

        private static void ShowClusters(Dictionary<int, List<BaseField>> clusters)
        {
            if (!(clustersFoldout = EditorGUILayout.Foldout(clustersFoldout, $"{clusters.Count} Clusters")))
                return;

            EditorGUI.indentLevel++;

            foreach (var kv in clusters.OrderByDescending(kv => kv.Value.Count))
            {
                var color = kv.Key;
                var list = kv.Value;

                var outputFields = list.Where(f => f is Node.OutputField).Cast<Node.OutputField>()
                    .Select(f => f.ToShortString()).ToList();

                EditorGUILayout.LabelField($"{list.Count} fields [{string.Join(", ", outputFields)}]", EditorStyles.boldLabel);

                foreach (var field in list)
                {
                    EditorGUI.indentLevel++;
                    ShowField(field);
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space(10);
            }

            EditorGUI.indentLevel--;
        }

        private static void ShowField(BaseField field)
        {
            using (new GUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(field.ToShortString());
                if (field is Node.OutputField outField)
                {
                    var origColor = GUI.backgroundColor;
                    if (GUILayout.Button("Go", EditorStyles.miniButton, GUILayout.Width(30)))
                    {
                        Selection.activeObject = outField.node;
                    }
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(field.GetValueAsString(), GUILayout.Width(80));
            }
        }
    }
}
