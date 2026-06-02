using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// Custom drawer for <see cref="DexterityEdge"/>. Phase 1 authoring UX before
    /// the new graph window lands.
    ///
    /// - target: dropdown of valid sink components on the SAME GameObject as the source
    ///   (the host's <see cref="GraphNode"/> + every <see cref="GraphOperator"/>
    ///   on the host). Hides ObjectField; designers can't accidentally point at a
    ///   foreign GO.
    /// - targetPort: only shown when target is a GraphNode. Dropdown of the node's
    ///   declared state-input port names (read live via SerializedProperty on the node).
    ///
    /// All writes route through <c>SerializedProperty</c> + <c>ApplyModifiedProperties</c>
    /// so Unity's prefab-override tracking sees them.
    /// </summary>
    [CustomPropertyDrawer(typeof(DexterityEdge))]
    public class DexterityEdgeDrawer : PropertyDrawer
    {
        private const float kRowHeight = 18f;
        private const float kRowSpacing = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var targetProp = property.FindPropertyRelative("target");
            var target = targetProp != null ? targetProp.objectReferenceValue : null;
            var rows = target is GraphNode ? 2 : 1;
            return rows * kRowHeight + (rows - 1) * kRowSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var targetProp = property.FindPropertyRelative("target");
            var portProp = property.FindPropertyRelative("targetPort");

            // Resolve the source component (the component this DexterityEdge belongs to).
            // serializedObject.targetObject is the owning component; its gameObject is the host.
            var sourceComponent = property.serializedObject.targetObject as Component;
            var hostGo = sourceComponent != null ? sourceComponent.gameObject : null;

            var targetRect = new Rect(position.x, position.y, position.width, kRowHeight);
            DrawTargetDropdown(targetRect, targetProp, sourceComponent, hostGo);

            if (targetProp.objectReferenceValue is GraphNode node)
            {
                var portRect = new Rect(position.x, position.y + kRowHeight + kRowSpacing,
                                        position.width, kRowHeight);
                DrawPortDropdown(portRect, portProp, node);
            }
            else if (!string.IsNullOrEmpty(portProp.stringValue))
            {
                // Target is not a node — clear any stale port name.
                portProp.stringValue = string.Empty;
            }

            EditorGUI.EndProperty();
        }

        private static void DrawTargetDropdown(Rect rect, SerializedProperty targetProp,
            Component sourceComponent, GameObject hostGo)
        {
            if (hostGo == null)
            {
                EditorGUI.PropertyField(rect, targetProp, new GUIContent("target"));
                return;
            }

            var sinks = new List<Component>();
            var labels = new List<string>();

            // Out node always first if present.
            if (hostGo.TryGetComponent<GraphNode>(out var outNode))
            {
                sinks.Add(outNode);
                labels.Add("Out (GraphNode)");
            }

            // Operators on host, in component order. Exclude the source itself (no self-edges).
            foreach (var agg in hostGo.GetComponents<GraphOperator>())
            {
                if (agg == sourceComponent) continue;
                sinks.Add(agg);
                labels.Add(agg.GetType().Name);
            }

            var current = targetProp.objectReferenceValue as Component;
            var selectedIdx = -1;
            for (var i = 0; i < sinks.Count; i++)
            {
                if (sinks[i] == current) { selectedIdx = i; break; }
            }

            // Add a leading "(none)" entry.
            var displayLabels = new string[sinks.Count + 1];
            displayLabels[0] = "(none)";
            for (var i = 0; i < labels.Count; i++) displayLabels[i + 1] = labels[i];

            var popupIdx = selectedIdx + 1;
            var newPopupIdx = EditorGUI.Popup(rect, "target", popupIdx, displayLabels);
            if (newPopupIdx != popupIdx)
            {
                targetProp.objectReferenceValue = newPopupIdx == 0 ? null : sinks[newPopupIdx - 1];
            }
        }

        private static void DrawPortDropdown(Rect rect, SerializedProperty portProp, GraphNode node)
        {
            // Read the node's ports via SerializedObject so we don't depend on node
            // instance state (works in edit mode regardless of OnEnable state). Edges can
            // target either a priority-resolved state port or a raw input port, so offer
            // both — input ports are tagged "(input)" in the dropdown while the stored
            // value stays the bare port name.
            var nodeSo = new SerializedObject(node);
            var portNames = new List<string>();
            var displayLabels = new List<string>();
            var statesProp = nodeSo.FindProperty("states");
            if (statesProp != null && statesProp.isArray)
            {
                for (var i = 0; i < statesProp.arraySize; i++)
                {
                    var name = statesProp.GetArrayElementAtIndex(i).stringValue;
                    if (string.IsNullOrEmpty(name)) continue;
                    portNames.Add(name);
                    displayLabels.Add(name);
                }
            }
            var inputsProp = nodeSo.FindProperty("inputs");
            if (inputsProp != null && inputsProp.isArray)
            {
                for (var i = 0; i < inputsProp.arraySize; i++)
                {
                    var name = inputsProp.GetArrayElementAtIndex(i).stringValue;
                    if (string.IsNullOrEmpty(name)) continue;
                    portNames.Add(name);
                    displayLabels.Add($"{name} (input)");
                }
            }

            if (portNames.Count == 0)
            {
                EditorGUI.LabelField(rect, "targetPort",
                    "(no ports declared on Out node)", EditorStyles.miniLabel);
                return;
            }

            var labels = new string[portNames.Count + 1];
            labels[0] = "(none)";
            for (var i = 0; i < portNames.Count; i++) labels[i + 1] = displayLabels[i];

            var currentIdx = 0;
            for (var i = 0; i < portNames.Count; i++)
            {
                if (portNames[i] == portProp.stringValue) { currentIdx = i + 1; break; }
            }

            var newIdx = EditorGUI.Popup(rect, "targetPort", currentIdx, labels);
            if (newIdx != currentIdx)
            {
                portProp.stringValue = newIdx == 0 ? string.Empty : portNames[newIdx - 1];
            }
        }
    }
}
