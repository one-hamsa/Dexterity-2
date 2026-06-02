using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    [CustomEditor(typeof(GraphNode)), CanEditMultipleObjects]
    public class GraphNodeEditor : BaseStateNodeEditor
    {
        private GraphNode node => (GraphNode)target;
        private ReorderableList _statesList;
        private ReorderableList _inputsList;

        public override void OnInspectorGUI()
        {
            Legacy_OnInspectorGUI();
        }

        protected override void ShowFields()
        {
            DrawAggregatedResult();

            EnsureLists();
            EditorGUI.BeginChangeCheck();
            _statesList.DoLayoutList();
            EditorGUILayout.Space(2);
            _inputsList.DoLayoutList();
            if (EditorGUI.EndChangeCheck())
            {
                // Commit before notifying so graph windows reading the underlying
                // GraphNode see the new lists immediately.
                serializedObject.ApplyModifiedProperties();
                DexterityGraphWindow.NotifyStateInputsEdited();
            }

            DrawOpenGraphButton();
        }

        // Two independent string ReorderableLists — one for priority-ordered states, one
        // for raw input ports. Each must persist across OnInspectorGUI calls: a
        // ReorderableList keeps its in-progress drag state on the instance, so rebuilding
        // it between the drag's mouse-down and mouse-up would discard the drag and the
        // reorder would never commit. SerializedObject is a stable instance for the
        // editor's lifetime (unlike the fresh SerializedProperty FindProperty hands back
        // each call), so compare against it to decide whether a rebuild is needed.
        private void EnsureLists()
        {
            if (_statesList?.serializedProperty != null
                && _statesList.serializedProperty.serializedObject == serializedObject)
                return;

            _statesList = BuildStringList("states", "States (priority high → low)",
                "Priority-ordered states. The first port with any active source wins and becomes the active state.");
            _inputsList = BuildStringList("inputs", "Inputs (raw signals)",
                "Raw input ports: wire-able and readable via GetRawInput(), but never a state and invisible to modifiers.");
        }

        private ReorderableList BuildStringList(string propName, string header, string headerTooltip)
        {
            var prop = serializedObject.FindProperty(propName);
            var list = new ReorderableList(serializedObject, prop, true, true, true, true);

            list.drawHeaderCallback = rect =>
                EditorGUI.LabelField(new Rect(rect.x + 14f, rect.y, rect.width - 14f, rect.height),
                    new GUIContent(header, headerTooltip));

            list.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                var arr = serializedObject.FindProperty(propName);
                if (index < 0 || index >= arr.arraySize) return;
                var row = new Rect(rect.x, rect.y + 1f, rect.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(row, arr.GetArrayElementAtIndex(index), GUIContent.none);
            };

            list.onAddCallback = l =>
            {
                var arr = serializedObject.FindProperty(propName);
                arr.InsertArrayElementAtIndex(arr.arraySize);
                arr.GetArrayElementAtIndex(arr.arraySize - 1).stringValue = "";
                l.index = arr.arraySize - 1;
            };

            return list;
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
