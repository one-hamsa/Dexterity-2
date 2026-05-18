using UnityEditor;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    [CustomEditor(typeof(GraphNode)), CanEditMultipleObjects]
    public class GraphNodeEditor : BaseStateNodeEditor
    {
        private GraphNode node => (GraphNode)target;

        public override void OnInspectorGUI()
        {
            Legacy_OnInspectorGUI();
        }

        protected override void ShowFields()
        {
            DrawAggregatedResult();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("stateInputs"));
            if (EditorGUI.EndChangeCheck())
            {
                // Commit before notifying so graph windows reading the underlying
                // GraphNode see the new list immediately.
                serializedObject.ApplyModifiedProperties();
                DexterityGraphWindow.NotifyStateInputsEdited();
            }

            DrawOpenGraphButton();
        }

        private void DrawOpenGraphButton()
        {
            if (targets.Length > 1) return;
            EditorGUILayout.Space(2);
            if (GUILayout.Button(new GUIContent("Open Graph",
                    "Open a graph window pinned to this node. Each click opens a new window — " +
                    "you can have several open at once for different nodes.")))
            {
                DexterityGraphWindow.OpenFor(node);
            }
        }

        private void DrawAggregatedResult()
        {
            string current;
            bool isRuntime = Application.IsPlaying(this);
            if (isRuntime)
            {
                current = node.GetActiveState() != -1
                    ? Database.instance.GetStateAsString(node.GetActiveState())
                    : null;
            }
            else
            {
                current = node.EvaluateTreeEditor() ?? node.initialState;
            }

            var style = new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14
            };

            // Tint with this node's current mode color (Off / Preview / Live).
            var modeColor = DexterityPreview.GetNodeColor(node);
            var modeLabel = DexterityPreview.GetNodeLabel(node);
            var origColor = GUI.color;
            GUI.color = string.IsNullOrEmpty(current) ? DexterityPreview.kNoneColor : modeColor;
            GUILayout.Label(
                string.IsNullOrEmpty(current)
                    ? $"(no state — {modeLabel})"
                    : $"{current}  [{modeLabel}]",
                style);
            GUI.color = origColor;
        }
    }
}
