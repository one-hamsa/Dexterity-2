using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// The three "modes" a single Dexterity node can be in. Orthogonal to the
    /// override registry — overrides are *values you force*, while the mode tells
    /// you *whether anything is rendering this node's state at all*.
    ///
    /// Per-node, not global: in a scene with many GraphNodes you might preview
    /// one and leave the rest in <see cref="None"/>.
    /// </summary>
    public enum DexterityPreviewMode
    {
        /// <summary>No one's rendering this node's state. Modifiers reflect last serialized values.</summary>
        None,
        /// <summary>Edit-time preview: this node is in the preview set (some graph window opted in)
        /// and the driver is animating Modifier transitions for it.</summary>
        Preview,
        /// <summary>The node is actually awake (initialized at runtime). Manager + Modifier
        /// loops are driving it directly — the editor driver stays out of the way.</summary>
        Live,
    }

    /// <summary>
    /// Tracks which <see cref="GraphNode"/>s are currently being previewed and
    /// computes the transitive "animatable set" (preview targets + everything they
    /// read from via <see cref="IDexteritySourceWithUpstreamNode"/>).
    ///
    /// Preview is opt-in per node. Graph windows call <see cref="AddTarget"/> when
    /// their "Preview" toggle is on. The driver only animates nodes in
    /// <see cref="AnimatableNodes"/> — in a 100-node scene with one window
    /// previewing one node, only one node (plus its upstream chain) is touched.
    /// </summary>
    public static class DexterityPreview
    {
        // Mode colors. Tuned to be distinct and readable on dark backgrounds.
        public static readonly Color kNoneColor    = new(0.55f, 0.55f, 0.55f);  // neutral gray
        public static readonly Color kPreviewColor = new(0.20f, 0.78f, 0.66f);  // cyan-green
        public static readonly Color kLiveColor    = new(0.86f, 0.22f, 0.52f);  // purplish red

        /// <summary>Fires when the set of preview targets or play state changes.</summary>
        public static event Action onChanged;

        // Refcounted preview-target set. Each graph window adds its target; multiple
        // windows targeting the same node bump the refcount.
        private static readonly Dictionary<GraphNode, int> s_targetCounts = new();
        // Transitive closure: preview targets + everything they depend on (upstream).
        private static readonly HashSet<GraphNode> s_animatable = new();

        public static IReadOnlyCollection<GraphNode> AnimatableNodes => s_animatable;

        /// <summary>Is this node in the animatable set (will the driver animate it)?</summary>
        public static bool IsAnimatable(GraphNode node) => node != null && s_animatable.Contains(node);

        /// <summary>
        /// Per-node mode. <see cref="DexterityPreviewMode.Live"/> wins whenever the
        /// node is initialized (correctly handles prefab-stage-in-play-mode: the
        /// node won't be initialized there, so it stays in Preview/None).
        /// </summary>
        public static DexterityPreviewMode GetNodeMode(GraphNode node)
        {
            if (node == null) return DexterityPreviewMode.None;
            if (node.initialized) return DexterityPreviewMode.Live;
            if (s_animatable.Contains(node)) return DexterityPreviewMode.Preview;
            return DexterityPreviewMode.None;
        }

        public static Color GetNodeColor(GraphNode node) => GetNodeMode(node) switch
        {
            DexterityPreviewMode.Live => kLiveColor,
            DexterityPreviewMode.Preview => kPreviewColor,
            _ => kNoneColor,
        };

        public static string GetNodeLabel(GraphNode node) => GetNodeMode(node) switch
        {
            DexterityPreviewMode.Live => "LIVE",
            DexterityPreviewMode.Preview => "PREVIEW",
            _ => "NO PREVIEW",
        };

        /// <summary>Add a node as a preview target. Refcounted — multiple callers for the same node OK.</summary>
        internal static void AddTarget(GraphNode node)
        {
            if (node == null) return;
            if (!s_targetCounts.TryGetValue(node, out var c))
                s_targetCounts[node] = 1;
            else
                s_targetCounts[node] = c + 1;
            RebuildAnimatable();
            onChanged?.Invoke();
        }

        /// <summary>Remove a node as a preview target. Refcounted — must be matched 1:1 with <see cref="AddTarget"/>.</summary>
        internal static void RemoveTarget(GraphNode node)
        {
            if (node == null) return;
            if (!s_targetCounts.TryGetValue(node, out var c)) return;
            if (c <= 1) s_targetCounts.Remove(node);
            else s_targetCounts[node] = c - 1;
            RebuildAnimatable();
            onChanged?.Invoke();
        }

        /// <summary>
        /// Recompute the preview set. Each target's <see cref="GraphNodePreviewRoot"/>
        /// (if any) expands to all GraphNodes in that group; targets without a
        /// preview root contribute only themselves.
        /// </summary>
        private static void RebuildAnimatable()
        {
            s_animatable.Clear();
            foreach (var kv in s_targetCounts)
            {
                var node = kv.Key;
                if (node == null) continue;
                var root = GraphNodePreviewRoot.FindTopMost(node.transform);
                if (root != null)
                {
                    foreach (var n in root.GetAllChildren())
                        if (n != null) s_animatable.Add(n);
                }
                else
                {
                    s_animatable.Add(node);
                }
            }

            // Sweep any null/destroyed targets.
            List<GraphNode> dead = null;
            foreach (var kv in s_targetCounts)
            {
                if (kv.Key == null) (dead ??= new()).Add(kv.Key);
            }
            if (dead != null)
                foreach (var d in dead) s_targetCounts.Remove(d);
        }

        [InitializeOnLoadMethod]
        private static void Init()
        {
            EditorApplication.playModeStateChanged += _ => onChanged?.Invoke();
        }
    }
}
