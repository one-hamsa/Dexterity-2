using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// Topological auto-layout for a GraphNode's host components — writes serialized
    /// <c>graphPosition</c> on every source (provider/aggregator) and on the node itself
    /// so the Dexterity Graph window opens to a clean left→right dependency view.
    ///
    /// Used by the graph window's "Re-layout" toolbar button, the GraphNode context-menu
    /// entry, and any procedural prefab-build script that wants a tidy graph without
    /// hand-positioning each box.
    ///
    /// Algorithm:
    /// 1. Collect all <see cref="GraphStateProvider"/> + <see cref="GraphAggregator"/>
    ///    components on the same GameObject as <c>node</c>.
    /// 2. Assign each source a layer = longest path from a root (a source with no
    ///    sibling source feeding it). Leaves are layer 0, aggregators settle on
    ///    layer 1+ based on what feeds them.
    /// 3. Order within each layer: layer 0 keeps <c>GetComponents</c> order; higher
    ///    layers sort by barycenter (mean row index of upstream sources in the layer
    ///    below) — produces short, untangled edges in the common case.
    /// 4. Position: x = layer * <c>columnSpacing</c>; y = row * <c>rowSpacing</c>,
    ///    each layer centred vertically against the densest layer so the tree looks
    ///    balanced. Node sits in the rightmost column at the vertical midpoint.
    /// 5. All writes go through <c>SerializedObject</c> + <c>ApplyModifiedProperties</c>
    ///    so prefab-override tracking stays correct (spike-verified — see
    ///    <c>Dexterity 2.0/CLAUDE.md</c>).
    /// </summary>
    public static class DexterityGraphLayout
    {
        /// <summary>Tunables for <see cref="AutoLayout"/>. Defaults match the 260×120 node size used by the graph window.</summary>
        public struct Options
        {
            [Tooltip("Horizontal distance between adjacent layers (px). Default 360 fits 260-wide nodes with breathing room.")]
            public float columnSpacing;

            [Tooltip("Vertical distance between adjacent rows within a layer (px). Default 160 fits 120-tall nodes with breathing room.")]
            public float rowSpacing;

            [Tooltip("Top-left of the graph (px). Default (40, 40).")]
            public Vector2 origin;

            [Tooltip("If true, layers above 0 sort their items by barycenter (mean row of upstream sources). Default true — yields short, untangled edges.")]
            public bool barycenterOrdering;

            public static Options Default => new Options {
                columnSpacing = 360f,
                rowSpacing    = 160f,
                origin        = new Vector2(40f, 40f),
                barycenterOrdering = true,
            };
        }

        /// <summary>Re-position every source on <paramref name="node"/>'s host GO plus the node itself.</summary>
        public static void AutoLayout(GraphNode node, Options opts)
        {
            if (node == null) return;
            if (opts.columnSpacing <= 0f) opts = Options.Default;

            var host = node.gameObject;
            var providers  = host.GetComponents<GraphStateProvider>();
            var aggregators = host.GetComponents<GraphAggregator>();

            // Order matters for the layer-0 "stable" ordering: providers first (sources
            // of truth, top-of-pipeline) then aggregators (which always end up in layers ≥ 1 anyway).
            var allSources = new List<Component>(providers.Length + aggregators.Length);
            foreach (var p in providers)   allSources.Add(p);
            foreach (var a in aggregators) allSources.Add(a);
            if (allSources.Count == 0)
            {
                // Just position the node and bail.
                WritePos(node, opts.origin);
                return;
            }

            // For each source S, who feeds S? (i.e. which siblings have an edge targeting S?)
            var sourceSet = new HashSet<Component>(allSources);
            var upstreamOf = new Dictionary<Component, List<Component>>(allSources.Count);
            foreach (var s in allSources) upstreamOf[s] = new List<Component>();

            foreach (var s in allSources)
            {
                var so = new SerializedObject(s);
                var outs = so.FindProperty("outputs");
                if (outs == null || !outs.isArray) continue;
                for (int i = 0; i < outs.arraySize; i++)
                {
                    var edge = outs.GetArrayElementAtIndex(i);
                    var target = edge.FindPropertyRelative("target").objectReferenceValue as Component;
                    if (target == null) continue;
                    if (target == node) continue;             // edge into Out node, not a sibling
                    if (!sourceSet.Contains(target)) continue; // dangling / off-host
                    upstreamOf[target].Add(s);
                }
            }

            // Longest-path layering. layer[s] = 0 if no sibling feeds it, else 1 + max(layer of upstream).
            var layer = new Dictionary<Component, int>(allSources.Count);
            int LayerOf(Component c)
            {
                if (layer.TryGetValue(c, out var v)) return v;
                // Defensive against cycles (shouldn't happen — the graph evaluator already
                // detects + errors on cycles — but be robust if user wires one mid-edit).
                layer[c] = 0;
                var ups = upstreamOf[c];
                int max = 0;
                for (int i = 0; i < ups.Count; i++)
                {
                    int u = LayerOf(ups[i]);
                    if (u + 1 > max) max = u + 1;
                }
                layer[c] = max;
                return max;
            }
            foreach (var s in allSources) LayerOf(s);

            int maxLayer = 0;
            foreach (var kv in layer) if (kv.Value > maxLayer) maxLayer = kv.Value;

            // Bucket by layer.
            var byLayer = new List<List<Component>>(maxLayer + 1);
            for (int i = 0; i <= maxLayer; i++) byLayer.Add(new List<Component>());
            foreach (var s in allSources) byLayer[layer[s]].Add(s);

            // Layer 0: keep the GetComponents discovery order (providers first, then aggregators —
            // but aggregators at layer 0 are unusual: an aggregator with no inputs).
            // Higher layers: barycenter by mean row index of upstream sources.
            if (opts.barycenterOrdering)
            {
                for (int L = 1; L <= maxLayer; L++)
                {
                    var current = byLayer[L];
                    // Snapshot upstream layer's order (must read before re-sorting this one).
                    var previousOrder = byLayer[L - 1];
                    current.Sort((a, b) => Barycenter(a, previousOrder, upstreamOf)
                                         .CompareTo(Barycenter(b, previousOrder, upstreamOf)));
                }
            }

            // Find the densest layer to center the others against.
            int maxRows = 0;
            foreach (var lyr in byLayer) if (lyr.Count > maxRows) maxRows = lyr.Count;

            // Position sources.
            for (int L = 0; L <= maxLayer; L++)
            {
                var items = byLayer[L];
                float yOffset = (maxRows - items.Count) * opts.rowSpacing * 0.5f;
                float x = opts.origin.x + L * opts.columnSpacing;
                for (int i = 0; i < items.Count; i++)
                {
                    float y = opts.origin.y + yOffset + i * opts.rowSpacing;
                    WritePos(items[i], new Vector2(x, y));
                }
            }

            // Node: rightmost column, vertically centered against the densest layer.
            float nodeX = opts.origin.x + (maxLayer + 1) * opts.columnSpacing;
            float nodeY = opts.origin.y + (maxRows - 1) * opts.rowSpacing * 0.5f;
            WritePos(node, new Vector2(nodeX, nodeY));
        }

        /// <summary>Convenience overload using <see cref="Options.Default"/>.</summary>
        public static void AutoLayout(GraphNode node) => AutoLayout(node, Options.Default);

        // Component context-menu entry — appears at the bottom of the GraphNode's gear
        // menu. Lives in the editor assembly because DexterityGraphLayout itself does.
        [MenuItem("CONTEXT/GraphNode/Re-layout graph sources")]
        private static void ContextMenu_AutoLayout(MenuCommand cmd)
        {
            var node = cmd.context as GraphNode;
            if (node == null) return;
            Undo.RegisterCompleteObjectUndo(node, "Re-layout graph sources");
            foreach (var p in node.GetComponents<GraphStateProvider>())
                Undo.RegisterCompleteObjectUndo(p, "Re-layout graph sources");
            foreach (var a in node.GetComponents<GraphAggregator>())
                Undo.RegisterCompleteObjectUndo(a, "Re-layout graph sources");
            AutoLayout(node);
        }

        private static float Barycenter(Component c, List<Component> previousLayer,
            Dictionary<Component, List<Component>> upstreamOf)
        {
            var ups = upstreamOf[c];
            if (ups.Count == 0) return 0f;
            float sum = 0f;
            int count = 0;
            for (int i = 0; i < ups.Count; i++)
            {
                int idx = previousLayer.IndexOf(ups[i]);
                if (idx >= 0) { sum += idx; count++; }
            }
            return count == 0 ? 0f : sum / count;
        }

        private static void WritePos(Component c, Vector2 pos)
        {
            var so = new SerializedObject(c);
            var prop = so.FindProperty("graphPosition");
            if (prop == null) return;
            prop.vector2Value = pos;
            so.ApplyModifiedProperties();
        }
    }
}
