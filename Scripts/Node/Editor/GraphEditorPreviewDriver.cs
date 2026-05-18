using System.Collections.Generic;
using System.Linq;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// Edit-time preview driver for GraphNodes.
    ///
    /// Animates Modifier transitions for the opt-in <see cref="DexterityPreview.AnimatableNodes"/>
    /// set. Skips nodes that are <see cref="BaseStateNode.initialized"/> = true — those are Live
    /// and the runtime Manager/Modifier loops drive them.
    ///
    /// <b>Why opt-in?</b> A scene may have hundreds of GraphNodes. Previewing every one
    /// on every override change wastes work. The driver now only touches nodes the user
    /// explicitly opted into via a graph window's Preview toggle (plus those nodes' upstream
    /// <see cref="IDexteritySourceWithUpstreamNode"/> dependencies).
    ///
    /// All transitions are serialized through a single coroutine because
    /// <see cref="EditorTransitions.TransitionAsync"/> owns the global Database singleton —
    /// concurrent calls would race over its create/destroy.
    /// </summary>
    internal static class GraphEditorPreviewDriver
    {
        private const float kPreviewSpeed = 6f;

        private class PendingTransition
        {
            public HashSet<Modifier> modifiers;
            public string fromState;
            public string toState;
        }

        private static readonly Dictionary<BaseStateNode, PendingTransition> s_pending = new();
        private static readonly Dictionary<GraphNode, string> s_renderedState = new();
        private static bool s_running;

        /// <summary>True while a Modifier transition coroutine is in flight.
        /// Other editor code can use this to throttle expensive inspector redraws.</summary>
        public static bool IsAnimating => s_running;

        /// <summary>Fires when <see cref="IsAnimating"/> flips.</summary>
        public static event System.Action onAnimatingChanged;

        [InitializeOnLoadMethod]
        private static void Init()
        {
            GraphPreviewOverrides.onChanged += ReevaluateAnimatableNodes;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosing += OnSceneClosing;
            DexterityPreview.onChanged += OnPreviewSetChanged;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            // New scene → drop stale baselines; the next preview registration will rebuild.
            s_renderedState.Clear();
        }

        private static void OnSceneClosing(Scene scene, bool removingScene)
        {
            var dead = new List<GraphNode>();
            foreach (var kv in s_renderedState)
                if (kv.Key == null) dead.Add(kv.Key);
            foreach (var k in dead) s_renderedState.Remove(k);
        }

        private static void OnPreviewSetChanged()
        {
            // Baseline newly-added animatable nodes from their current state so the user's
            // first interaction with them animates correctly. Existing baselines stay.
            foreach (var node in DexterityPreview.AnimatableNodes)
            {
                if (node == null) continue;
                if (node.initialized) continue;          // Live — runtime owns the visual
                if (s_renderedState.ContainsKey(node)) continue;
                s_renderedState[node] = node.EvaluateTreeEditor() ?? node.initialState;
            }
        }

        private static readonly HashSet<Modifier> s_modScratch = new();

        /// <summary>
        /// Returns animatable modifiers attached to <paramref name="node"/>. Shares
        /// the discovery path with FieldNode previews via
        /// <see cref="BaseStateNodeEditor.GetModifiers(BaseStateNode)"/> — runtime
        /// uses the registered <c>nodeModifiers</c> set, edit-time falls back to a
        /// scene scan filtered by <c>GetNode() == node</c>. We further filter by
        /// <see cref="Modifier.animatableInEditor"/> since the preview driver only
        /// animates the ones flagged that way.
        /// </summary>
        private static HashSet<Modifier> CollectAnimatableModifiers(GraphNode node)
        {
            s_modScratch.Clear();
            foreach (var m in BaseStateNodeEditor.GetModifiers(node))
            {
                if (!m.animatableInEditor) continue;
                s_modScratch.Add(m);
            }
            return s_modScratch;
        }

        private static void ReevaluateAnimatableNodes()
        {
            // No-op unless something is actually being previewed.
            if (DexterityPreview.AnimatableNodes.Count == 0) return;

            foreach (var node in DexterityPreview.AnimatableNodes)
            {
                if (node == null) continue;
                if (node.initialized) continue;          // Live — runtime owns it

                var newState = node.EvaluateTreeEditor() ?? node.initialState;

                if (!s_renderedState.TryGetValue(node, out var prev))
                {
                    // First sighting (rare — preview-set entries are usually baselined).
                    s_renderedState[node] = newState;
                    continue;
                }
                if (newState == prev) continue;

                s_renderedState[node] = newState;

                // Per-node subtree scan — avoids the previous scene-global Resources scan.
                var modifiers = CollectAnimatableModifiers(node);
                if (modifiers.Count == 0) continue;
                EnqueueTransition(node, new HashSet<Modifier>(modifiers), prev, newState);
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

        private static void SetRunning(bool value)
        {
            if (s_running == value) return;
            s_running = value;
            onAnimatingChanged?.Invoke();
        }

        private static void Pump()
        {
            if (s_pending.Count == 0)
            {
                SetRunning(false);
                return;
            }

            SetRunning(true);

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
