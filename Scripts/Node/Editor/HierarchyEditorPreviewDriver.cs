using System.Collections.Generic;
using System.Linq;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// Edit-time preview driver for HierarchyNodes. Subscribes once to
    /// <see cref="HierarchyPreviewOverrides.onChanged"/> and, on every change,
    /// re-evaluates every <see cref="HierarchyNode"/> in the scene, queueing
    /// a Modifier transition for any whose state shifted.
    ///
    /// All transitions are serialized through a single coroutine because
    /// <see cref="EditorTransitions.TransitionAsync"/> owns the global Database
    /// singleton — concurrent calls would race over its create/destroy.
    /// </summary>
    internal static class HierarchyEditorPreviewDriver
    {
        private const float kPreviewSpeed = 6f;

        private class PendingTransition
        {
            public HashSet<Modifier> modifiers;
            public string fromState;
            public string toState;
        }

        private static readonly Dictionary<BaseStateNode, PendingTransition> s_pending = new();
        private static readonly Dictionary<HierarchyNode, string> s_renderedState = new();
        private static bool s_running;

        [InitializeOnLoadMethod]
        private static void Init()
        {
            HierarchyPreviewOverrides.onChanged += ReevaluateAllNodes;
        }

        private static void ReevaluateAllNodes()
        {
            if (Application.isPlaying) return;

            // Group all animatable modifiers in the scene by their owning node.
            var modByNode = new Dictionary<BaseStateNode, HashSet<Modifier>>();
            foreach (var m in Resources.FindObjectsOfTypeAll<Modifier>())
            {
                if (m.gameObject.hideFlags != HideFlags.None) continue;
                if (!m.animatableInEditor) continue;
                var node = m.GetNode();
                if (node == null) continue;
                if (!modByNode.TryGetValue(node, out var set))
                    modByNode[node] = set = new HashSet<Modifier>();
                set.Add(m);
            }

            foreach (var node in Object.FindObjectsOfType<HierarchyNode>())
            {
                var newState = node.EvaluateTreeEditor() ?? node.initialState;

                if (!s_renderedState.TryGetValue(node, out var prev))
                {
                    s_renderedState[node] = newState;
                    continue;
                }
                if (newState == prev) continue;

                s_renderedState[node] = newState;

                if (!modByNode.TryGetValue(node, out var modifiers) || modifiers.Count == 0)
                    continue;

                EnqueueTransition(node, modifiers, prev, newState);
            }
        }

        private static void EnqueueTransition(BaseStateNode owner, HashSet<Modifier> modifiers, string fromState, string toState)
        {
            if (string.IsNullOrEmpty(toState)) return;

            s_pending[owner] = new PendingTransition
            {
                modifiers = modifiers,
                fromState = string.IsNullOrEmpty(fromState) ? toState : fromState,
                toState = toState,
            };

            if (!s_running) Pump();
        }

        private static void Pump()
        {
            if (s_pending.Count == 0)
            {
                s_running = false;
                return;
            }

            s_running = true;

            var owner = s_pending.Keys.First();
            var req = s_pending[owner];
            s_pending.Remove(owner);

            EditorCoroutineUtility.StartCoroutineOwnerless(
                EditorTransitions.TransitionAsync(
                    req.modifiers, req.fromState, req.toState,
                    speed: kPreviewSpeed,
                    onEnd: Pump));
        }
    }
}
