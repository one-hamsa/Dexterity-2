using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    [CustomEditor(typeof(GraphNode)), CanEditMultipleObjects]
    public class GraphNodeEditor : BaseStateNodeEditor
    {
        private GraphNode node => (GraphNode)target;
        private ReorderableList _stateInputsList;

        public override void OnInspectorGUI()
        {
            Legacy_OnInspectorGUI();
        }

        protected override void ShowFields()
        {
            DrawAggregatedResult();

            EnsureStateInputsList();
            EditorGUI.BeginChangeCheck();
            _stateInputsList.DoLayoutList();
            if (EditorGUI.EndChangeCheck())
            {
                // Commit before notifying so graph windows reading the underlying
                // GraphNode see the new list immediately.
                serializedObject.ApplyModifiedProperties();
                DexterityGraphWindow.NotifyStateInputsEdited();
            }

            DrawOpenGraphButton();
        }

        // ReorderableList over `stateInputs` that also draws + maintains the parallel
        // `stateInputsRawOnly` bool list. We keep two SerializedProperty arrays — the
        // alternative (a struct with custom drawer) would break existing serialized
        // prefabs. The cost is that reorder/add/remove must update both lists in
        // lock-step; the callbacks below do exactly that.
        private void EnsureStateInputsList()
        {
            if (_stateInputsList != null && _stateInputsList.serializedProperty == serializedObject.FindProperty("stateInputs"))
                return;

            var inputs = serializedObject.FindProperty("stateInputs");
            _stateInputsList = new ReorderableList(serializedObject, inputs, true, true, true, true);

            _stateInputsList.drawHeaderCallback = rect =>
            {
                const float rawColWidth = 56f;
                var nameRect = new Rect(rect.x + 14f, rect.y, rect.width - rawColWidth - 18f, rect.height);
                var rawRect  = new Rect(rect.xMax - rawColWidth, rect.y, rawColWidth, rect.height);
                EditorGUI.LabelField(nameRect, "State Inputs (priority high → low)");
                EditorGUI.LabelField(rawRect, new GUIContent("Raw",
                    "Raw-only: wire-able and readable via GetRawInput, but excluded from modifiers and never the active state."));
            };

            _stateInputsList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                EnsureRawListMatches();
                var inputsArr = serializedObject.FindProperty("stateInputs");
                var rawArr    = serializedObject.FindProperty("stateInputsRawOnly");
                if (index < 0 || index >= inputsArr.arraySize) return;

                const float rawColWidth = 56f;
                const float pad = 4f;
                var row = new Rect(rect.x, rect.y + 1f, rect.width, EditorGUIUtility.singleLineHeight);
                var nameRect = new Rect(row.x, row.y, row.width - rawColWidth - pad, row.height);
                var rawRect  = new Rect(row.xMax - rawColWidth, row.y, rawColWidth, row.height);

                var nameProp = inputsArr.GetArrayElementAtIndex(index);
                EditorGUI.PropertyField(nameRect, nameProp, GUIContent.none);

                if (index < rawArr.arraySize)
                {
                    var rawProp = rawArr.GetArrayElementAtIndex(index);
                    var toggleRect = new Rect(rawRect.x + (rawColWidth - 16f) * 0.5f, rawRect.y, 16f, rawRect.height);
                    rawProp.boolValue = EditorGUI.Toggle(toggleRect, rawProp.boolValue);
                }
            };

            _stateInputsList.onAddCallback = list =>
            {
                EnsureRawListMatches();
                var inputsArr = serializedObject.FindProperty("stateInputs");
                var rawArr    = serializedObject.FindProperty("stateInputsRawOnly");
                inputsArr.InsertArrayElementAtIndex(inputsArr.arraySize);
                inputsArr.GetArrayElementAtIndex(inputsArr.arraySize - 1).stringValue = "";
                rawArr.InsertArrayElementAtIndex(rawArr.arraySize);
                rawArr.GetArrayElementAtIndex(rawArr.arraySize - 1).boolValue = false;
                list.index = inputsArr.arraySize - 1;
            };

            _stateInputsList.onRemoveCallback = list =>
            {
                EnsureRawListMatches();
                var inputsArr = serializedObject.FindProperty("stateInputs");
                var rawArr    = serializedObject.FindProperty("stateInputsRawOnly");
                int idx = list.index >= 0 ? list.index : inputsArr.arraySize - 1;
                if (idx < 0 || idx >= inputsArr.arraySize) return;
                inputsArr.DeleteArrayElementAtIndex(idx);
                if (idx < rawArr.arraySize) rawArr.DeleteArrayElementAtIndex(idx);
                list.index = Mathf.Min(idx, inputsArr.arraySize - 1);
            };

            // Reorder both arrays in lock-step. Unity's ReorderableList has already
            // moved the inputs array by the time this fires — we just need to apply
            // the same move to the parallel raw-only array.
            _stateInputsList.onReorderCallbackWithDetails = (list, oldIndex, newIndex) =>
            {
                var rawArr = serializedObject.FindProperty("stateInputsRawOnly");
                if (oldIndex < 0 || oldIndex >= rawArr.arraySize) return;
                if (newIndex < 0 || newIndex >= rawArr.arraySize) return;
                rawArr.MoveArrayElement(oldIndex, newIndex);
            };
        }

        // Defensive: if the user edits the prefab outside this inspector (e.g. via a
        // procedural script that only writes stateInputs), the raw-only list can fall
        // out of sync. OnValidate on the GraphNode normalises in-editor, but we also
        // pad here so the inspector's per-row toggle never indexes out of range.
        private void EnsureRawListMatches()
        {
            var inputsArr = serializedObject.FindProperty("stateInputs");
            var rawArr    = serializedObject.FindProperty("stateInputsRawOnly");
            while (rawArr.arraySize < inputsArr.arraySize) rawArr.InsertArrayElementAtIndex(rawArr.arraySize);
            while (rawArr.arraySize > inputsArr.arraySize) rawArr.DeleteArrayElementAtIndex(rawArr.arraySize - 1);
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
