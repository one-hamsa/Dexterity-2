using System.Collections.Generic;
using System.Linq;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// Interactive graph view for a <see cref="HierarchyNode"/>'s provider tree.
    /// Left-to-right tree layout, shoulder-style edges, per-leaf 3-state IsActive
    /// override (yes / no / none). While open it auto-drives Modifiers via
    /// <see cref="EditorTransitions"/> when the aggregated state changes.
    /// </summary>
    public class HierarchyGraphWindow : EditorWindow
    {
        [MenuItem("Tools/Dexterity/Hierarchy Graph")]
        private static void ShowEmpty()
        {
            var w = GetWindow<HierarchyGraphWindow>("Hierarchy Graph");
            w.Show();
        }

        public static void OpenFor(HierarchyNode node)
        {
            var w = GetWindow<HierarchyGraphWindow>("Hierarchy Graph");
            w._target = node;
            w._lockTarget = true;
            w.Rebuild();
            w.Focus();
        }

        /// <summary>
        /// Like <see cref="OpenFor"/>, but always spawns a fresh window
        /// (used by ghost-box clicks so the current graph stays visible).
        /// </summary>
        public static void OpenForInNewWindow(HierarchyNode node)
        {
            var w = CreateWindow<HierarchyGraphWindow>("Hierarchy Graph");
            w._target = node;
            w._lockTarget = true;
            w.Rebuild();
            w.Show();
            w.Focus();
        }

        // ─── Target ───────────────────────────────────────────────────────
        private HierarchyNode _target;
        private bool _lockTarget;

        // ─── Graph model ──────────────────────────────────────────────────
        private class Box
        {
            public Component component;          // HierarchyNode | HierarchyAggregator | HierarchyStateProvider
            public int depth;
            public Rect rect;                    // graph-space
            public float subtreeHeight;
            public bool isExternal;              // ghost box: a node referenced from outside this tree
        }
        private readonly List<Box> _boxes = new();
        private readonly List<(Box from, Box to)> _edges = new();
        private Box _selected;

        // ─── View ─────────────────────────────────────────────────────────
        private Vector2 _pan;
        private float _zoom = 1f;
        private const float kToolbarH = 22f;
        private const float kBoxW = 220f;
        private const float kBoxH = 64f;
        private const float kGapX = 90f;
        private const float kGapY = 14f;

        // ─── Live preview ─────────────────────────────────────────────────
        // Transitions are owned by HierarchyEditorPreviewDriver — the window
        // only re-subscribes to provider changes so it can Repaint.
        private readonly HashSet<IHierarchyStateProvider> _subscribed = new();

        // Counted across all open windows — overrides clear only when the last
        // one closes, so opening a second window via a ghost click doesn't
        // wipe the state another window is showing.
        private static int s_openCount;

        private void OnEnable()
        {
            wantsMouseMove = true;   // so pill hover-tint can repaint
            s_openCount++;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            Selection.selectionChanged += OnSelectionChanged;
            HierarchyPreviewOverrides.onChanged += OnOverridesChanged;
            OnSelectionChanged();
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            Selection.selectionChanged -= OnSelectionChanged;
            HierarchyPreviewOverrides.onChanged -= OnOverridesChanged;
            UnsubscribeFromAll();

            s_openCount--;
            if (s_openCount <= 0)
                HierarchyPreviewOverrides.ClearAll();
        }

        private void OnSelectionChanged()
        {
            if (_lockTarget) return;
            var go = Selection.activeGameObject;
            if (go == null) return;

            var node = go.GetComponentInParent<HierarchyNode>();
            if (node != null && node != _target)
            {
                _target = node;
                Rebuild();
                Repaint();
            }
        }

        private void OnHierarchyChanged()
        {
            Rebuild();
            Repaint();
        }

        private void OnOverridesChanged()
        {
            // Driver handles the modifier transitions; window just repaints.
            Repaint();
        }

        // ─── Graph build ──────────────────────────────────────────────────

        private void Rebuild()
        {
            _boxes.Clear();
            _edges.Clear();
            UnsubscribeFromAll();

            if (_target == null) return;

            var root = new Box { component = _target, depth = 0 };
            _boxes.Add(root);
            BuildSubtree(_target.transform, root, 1);
            LayoutBoxes(root);
            AddCrossRefGhosts();

            SubscribeToAllProviders();
        }

        /// <summary>
        /// Post-pass: every NodeStateProvider in the tree that points at a
        /// HierarchyNode outside this graph becomes an edge to a "ghost" box
        /// representing that node. Ghosts are pinned one column past the
        /// deepest tree leaf and centered on the average y of their referrers.
        /// </summary>
        private void AddCrossRefGhosts()
        {
            var ghosts = new Dictionary<HierarchyNode, Box>();
            foreach (var b in _boxes.ToList())
            {
                if (b.component is Builtins.NodeStateProvider nsp
                    && nsp.targetNode is HierarchyNode hn
                    && hn != _target)
                {
                    if (!ghosts.TryGetValue(hn, out var ghost))
                    {
                        ghost = new Box { component = hn, isExternal = true };
                        ghosts[hn] = ghost;
                        _boxes.Add(ghost);
                    }
                    _edges.Add((b, ghost));
                }
            }

            if (ghosts.Count == 0) return;

            var ghostDepth = _boxes.Where(b => !b.isExternal).Max(b => b.depth) + 1;
            foreach (var (_, ghost) in ghosts)
            {
                ghost.depth = ghostDepth;
                var y = _edges.Where(e => e.to == ghost).Select(e => e.from.rect.center.y).Average();
                ghost.rect = new Rect(ghostDepth * (kBoxW + kGapX), y - kBoxH * 0.5f, kBoxW, kBoxH);
            }

            // Vertical deconflict — push overlapping ghosts down.
            var sorted = ghosts.Values.OrderBy(g => g.rect.y).ToList();
            for (var i = 1; i < sorted.Count; i++)
            {
                var minTop = sorted[i - 1].rect.yMax + kGapY;
                if (sorted[i].rect.y < minTop)
                    sorted[i].rect = new Rect(sorted[i].rect.x, minTop, kBoxW, kBoxH);
            }
        }

        private void BuildSubtree(Transform parent, Box parentBox, int depth)
        {
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);

                if (child.TryGetComponent<HierarchyAggregator>(out var agg))
                {
                    var b = new Box { component = agg, depth = depth };
                    _boxes.Add(b);
                    _edges.Add((parentBox, b));
                    BuildSubtree(child, b, depth + 1);
                    continue;
                }

                if (child.TryGetComponent<HierarchyNode>(out _))
                    continue;

                if (child.TryGetComponent<HierarchyStateProvider>(out var leaf))
                {
                    var b = new Box { component = leaf, depth = depth };
                    _boxes.Add(b);
                    _edges.Add((parentBox, b));
                }

                // recurse for "transform-only" GameObjects (no provider on this GO)
                BuildSubtree(child, parentBox, depth);
            }
        }

        private void LayoutBoxes(Box root)
        {
            var children = new Dictionary<Box, List<Box>>();
            foreach (var b in _boxes) children[b] = new List<Box>();
            foreach (var (f, t) in _edges) children[f].Add(t);

            float ComputeSubtreeHeight(Box b)
            {
                if (children[b].Count == 0) return b.subtreeHeight = kBoxH;
                float sum = 0;
                foreach (var c in children[b]) sum += ComputeSubtreeHeight(c) + kGapY;
                sum -= kGapY;
                return b.subtreeHeight = Mathf.Max(kBoxH, sum);
            }

            ComputeSubtreeHeight(root);

            void Position(Box b, float yCenter)
            {
                b.rect = new Rect(b.depth * (kBoxW + kGapX), yCenter - kBoxH * 0.5f, kBoxW, kBoxH);
                if (children[b].Count == 0) return;
                var yCursor = yCenter - b.subtreeHeight * 0.5f;
                foreach (var c in children[b])
                {
                    Position(c, yCursor + c.subtreeHeight * 0.5f);
                    yCursor += c.subtreeHeight + kGapY;
                }
            }

            Position(root, root.subtreeHeight * 0.5f);
        }

        // ─── GUI ──────────────────────────────────────────────────────────

        private void OnGUI()
        {
            DrawToolbar();
            HandleInput();

            var graphRect = new Rect(0, kToolbarH, position.width, position.height - kToolbarH);
            EditorGUI.DrawRect(graphRect, new Color(0.20f, 0.21f, 0.24f));

            if (_target == null)
            {
                GUI.Label(graphRect, "Select a GameObject with a HierarchyNode.",
                    new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter });
                return;
            }

            // edges below boxes
            Handles.BeginGUI();
            foreach (var (f, t) in _edges)
                DrawShoulderEdge(f, t);
            Handles.EndGUI();

            foreach (var b in _boxes)
                DrawBox(b);

            DrawAggregatedResultBanner();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Target", EditorStyles.miniLabel, GUILayout.Width(46));

                EditorGUI.BeginChangeCheck();
                var newTarget = (HierarchyNode)EditorGUILayout.ObjectField(
                    _target, typeof(HierarchyNode), true, GUILayout.MinWidth(180));
                if (EditorGUI.EndChangeCheck())
                {
                    _target = newTarget;
                    Rebuild();
                }

                _lockTarget = GUILayout.Toggle(_lockTarget,
                    new GUIContent("🔒", "Lock target — don't follow Selection."),
                    EditorStyles.toolbarButton, GUILayout.Width(28));

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Clear Overrides", EditorStyles.toolbarButton, GUILayout.Width(110)))
                    HierarchyPreviewOverrides.ClearAll();

                if (GUILayout.Button("Fit", EditorStyles.toolbarButton, GUILayout.Width(40)))
                    FitView();

                if (GUILayout.Button("Reset", EditorStyles.toolbarButton, GUILayout.Width(50)))
                {
                    _pan = Vector2.zero;
                    _zoom = 1f;
                }

                GUILayout.Label($"x{_zoom:0.00}", EditorStyles.miniLabel, GUILayout.Width(46));
            }
        }

        private void HandleInput()
        {
            var e = Event.current;
            var graphRect = new Rect(0, kToolbarH, position.width, position.height - kToolbarH);

            if (e.type == EventType.MouseDrag && (e.button == 2 || (e.button == 0 && e.alt)))
            {
                if (graphRect.Contains(e.mousePosition))
                {
                    _pan += e.delta;
                    Repaint();
                    e.Use();
                }
            }

            if (e.type == EventType.ScrollWheel && graphRect.Contains(e.mousePosition))
            {
                var oldZoom = _zoom;
                _zoom = Mathf.Clamp(_zoom * (1f - e.delta.y * 0.04f), 0.35f, 2.0f);
                // zoom around mouse
                var mouseGraphPos = (e.mousePosition - _pan - new Vector2(0, kToolbarH)) / oldZoom;
                _pan = e.mousePosition - new Vector2(0, kToolbarH) - mouseGraphPos * _zoom;
                Repaint();
                e.Use();
            }
        }

        private void FitView()
        {
            if (_boxes.Count == 0) { _pan = Vector2.zero; _zoom = 1f; return; }

            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var b in _boxes)
            {
                min = Vector2.Min(min, b.rect.min);
                max = Vector2.Max(max, b.rect.max);
            }

            var contentSize = max - min;
            var pad = new Vector2(40, 40);
            var available = new Vector2(position.width, position.height - kToolbarH) - pad * 2;
            _zoom = Mathf.Clamp(Mathf.Min(available.x / contentSize.x, available.y / contentSize.y), 0.35f, 2.0f);

            var contentCenter = (min + max) * 0.5f;
            _pan = -contentCenter * _zoom + new Vector2(position.width * 0.5f, (position.height - kToolbarH) * 0.5f);
        }

        // ─── Drawing ──────────────────────────────────────────────────────

        private Vector2 ToScreen(Vector2 g) => g * _zoom + _pan + new Vector2(20, kToolbarH + 20);
        private Rect ToScreen(Rect r) => new Rect(ToScreen(r.position), r.size * _zoom);

        private void DrawShoulderEdge(Box from, Box to)
        {
            var p1 = ToScreen(new Vector2(from.rect.xMax, from.rect.center.y));
            var p4 = ToScreen(new Vector2(to.rect.xMin, to.rect.center.y));

            // Use the gap between columns for the elbow so edges don't overlap boxes.
            var midX = (p1.x + p4.x) * 0.5f;
            var p2 = new Vector2(midX, p1.y);
            var p3 = new Vector2(midX, p4.y);

            bool incidentToSelected = _selected != null && (_selected == from || _selected == to);
            Handles.color = incidentToSelected
                ? new Color(1f, 0.86f, 0.32f, 1f)
                : to.isExternal
                    ? new Color(0.95f, 0.66f, 0.30f, 0.85f)  // orange — cross-ref edge
                    : new Color(0.62f, 0.66f, 0.72f, 0.85f);
            Handles.DrawAAPolyLine(incidentToSelected ? 3f : 2f, p1, p2, p3, p4);
        }

        private void DrawBox(Box b)
        {
            var rect = ToScreen(b.rect);
            var (bg, border) = BoxColors(b);
            var leaf = b.component as HierarchyStateProvider;

            // Override pill rect — comfortably sized, taking up the bottom strip
            // of the box so it's easy to hit without aiming for a tiny target.
            var pillRect = leaf != null
                ? new Rect(rect.xMax - 64, rect.yMax - 28, 56, 24)
                : Rect.zero;

            // ── Event handling FIRST (before drawing), in z-order priority:
            //    pill → box selection.
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (leaf != null && pillRect.Contains(e.mousePosition))
                {
                    CycleOverride(leaf);
                    e.Use();
                }
                else if (rect.Contains(e.mousePosition))
                {
                    // Click a ghost → open the target's graph in a NEW window so
                    // the current view stays put; otherwise select.
                    if (b.isExternal && b.component is HierarchyNode ext)
                        OpenForInNewWindow(ext);
                    else
                    {
                        _selected = b;
                        Selection.activeObject = b.component;
                    }
                    Repaint();
                    e.Use();
                }
            }

            // ── Drawing.
            if (_selected == b)
            {
                var glow = new Rect(rect.x - 3, rect.y - 3, rect.width + 6, rect.height + 6);
                EditorGUI.DrawRect(glow, new Color(1f, 0.78f, 0.25f, 0.95f));
            }

            EditorGUI.DrawRect(rect, bg);
            DrawBorder(rect, border, _selected == b ? 2 : 1);

            // title row
            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = Mathf.RoundToInt(Mathf.Lerp(9f, 12f, Mathf.InverseLerp(0.4f, 1.2f, _zoom))),
                normal = { textColor = new Color(0.96f, 0.96f, 0.97f) }
            };
            GUI.Label(new Rect(rect.x + 8, rect.y + 4, rect.width - 16, 18), b.component.name, titleStyle);

            // Subtitle row — type name, OR (for NodeStateProvider) the cross-ref
            // "← TargetNode.TargetState" since that's the more useful info there.
            // Clicking the cross-ref jumps the graph to the target node.
            var subRect = new Rect(rect.x + 8, rect.y + 20, rect.width - 16, 14);
            var isCrossRef = b.component is Builtins.NodeStateProvider nsp && nsp.targetNode != null;
            if (isCrossRef)
            {
                var nspRef = (Builtins.NodeStateProvider)b.component;
                var refStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontSize = Mathf.RoundToInt(Mathf.Lerp(8f, 10f, Mathf.InverseLerp(0.4f, 1.2f, _zoom))),
                    normal = { textColor = new Color(0.62f, 0.80f, 1.00f) },
                    fontStyle = FontStyle.Italic,
                };
                GUI.Label(subRect, $"\u2190 {nspRef.targetNode.name}.{nspRef.targetState}", refStyle);
                EditorGUIUtility.AddCursorRect(subRect, MouseCursor.Link);
                if (e.type == EventType.MouseDown && e.button == 0 && subRect.Contains(e.mousePosition))
                {
                    if (nspRef.targetNode is HierarchyNode target)
                        OpenForInNewWindow(target);
                    else
                        Selection.activeObject = nspRef.targetNode;
                    e.Use();
                }
            }
            else
            {
                var typeStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontSize = Mathf.RoundToInt(Mathf.Lerp(8f, 10f, Mathf.InverseLerp(0.4f, 1.2f, _zoom))),
                    normal = { textColor = new Color(0.70f, 0.75f, 0.82f) }
                };
                GUI.Label(subRect, b.component.GetType().Name, typeStyle);
            }

            // state row
            var stateText = BoxStateText(b);
            var stateColor = BoxStateColor(b);
            var stateStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = Mathf.RoundToInt(Mathf.Lerp(9f, 11f, Mathf.InverseLerp(0.4f, 1.2f, _zoom))),
                normal = { textColor = stateColor },
                fontStyle = FontStyle.Bold
            };
            GUI.Label(new Rect(rect.x + 8, rect.y + 36, rect.width - 72, 18), stateText, stateStyle);

            // Pill visual (hover-tinted) — drawn last so it sits above the box.
            if (leaf != null)
                DrawOverridePill(pillRect, leaf, e);
        }

        private static void CycleOverride(HierarchyStateProvider leaf)
        {
            var has = HierarchyPreviewOverrides.TryGet(leaf, out var ov);
            if (!has) HierarchyPreviewOverrides.Set(leaf, true);
            else if (ov) HierarchyPreviewOverrides.Set(leaf, false);
            else HierarchyPreviewOverrides.Clear(leaf);
        }

        private void DrawOverridePill(Rect pillRect, HierarchyStateProvider leaf, Event e)
        {
            var hasOverride = HierarchyPreviewOverrides.TryGet(leaf, out var ov);
            string label;
            Color bg;
            if (!hasOverride) { label = "—";   bg = new Color(0.40f, 0.42f, 0.46f); }
            else if (ov)      { label = "ON";  bg = new Color(0.30f, 0.78f, 0.42f); }
            else              { label = "OFF"; bg = new Color(0.85f, 0.32f, 0.32f); }

            // Hover tint — mouseover lightens the pill so the affordance reads
            // even before the click.
            if (pillRect.Contains(e.mousePosition))
                bg = Color.Lerp(bg, Color.white, 0.18f);

            EditorGUI.DrawRect(pillRect, bg);
            DrawBorder(pillRect, new Color(0, 0, 0, 0.55f), 1);

            var style = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                fontSize = 11,
            };
            GUI.Label(pillRect, label, style);

            EditorGUIUtility.AddCursorRect(pillRect, MouseCursor.Link);

            // Force a repaint on hover so the tint updates without needing
            // mouse-motion outside the pill to trigger one.
            if (e.type == EventType.MouseMove && pillRect.Contains(e.mousePosition))
                Repaint();
        }

        private void DrawAggregatedResultBanner()
        {
            var current = Application.isPlaying && _target.GetActiveState() != -1
                ? Database.instance.GetStateAsString(_target.GetActiveState())
                : _target.EvaluateTreeEditor() ?? _target.initialState;

            var bannerRect = new Rect(8, kToolbarH + 6, 280, 26);
            var bg = new Color(0.12f, 0.13f, 0.16f, 0.92f);
            EditorGUI.DrawRect(bannerRect, bg);
            DrawBorder(bannerRect, new Color(0.45f, 0.55f, 0.65f, 0.9f), 1);

            var style = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.45f, 0.92f, 0.55f) },
                fontSize = 12,
            };
            GUI.Label(bannerRect, $"State: {(string.IsNullOrEmpty(current) ? "(none)" : current)}", style);
        }

        private (Color bg, Color border) BoxColors(Box b)
        {
            // Ghosts: dimmed fill, dashed-feeling orange border to read as "elsewhere".
            if (b.isExternal)
                return (new Color(0.18f, 0.20f, 0.26f, 0.85f), new Color(0.95f, 0.66f, 0.30f, 0.75f));

            return b.component switch
            {
                HierarchyNode _ => (new Color(0.20f, 0.30f, 0.42f), new Color(0.45f, 0.65f, 0.85f, 0.95f)),
                HierarchyAggregator _ => (new Color(0.32f, 0.27f, 0.18f), new Color(0.85f, 0.70f, 0.30f, 0.85f)),
                HierarchyStateProvider _ => (new Color(0.22f, 0.28f, 0.22f), new Color(0.45f, 0.75f, 0.50f, 0.85f)),
                _ => (Color.gray, Color.black)
            };
        }

        private string BoxStateText(Box b)
        {
            switch (b.component)
            {
                case HierarchyNode node:
                {
                    var s = Application.isPlaying && node.GetActiveState() != -1
                        ? Database.instance.GetStateAsString(node.GetActiveState())
                        : node.EvaluateTreeEditor();
                    return string.IsNullOrEmpty(s) ? "→ " + node.initialState : "→ " + s;
                }
                case HierarchyAggregator agg:
                    return agg.TryGetState(out var aggState) ? "→ " + aggState : "(idle)";
                case HierarchyStateProvider leaf:
                {
                    var active = leaf.IsActive;
                    var prefix = active ? "● " : "○ ";
                    return prefix + (string.IsNullOrEmpty(leaf.State) ? "(no state)" : leaf.State);
                }
                default:
                    return "";
            }
        }

        private Color BoxStateColor(Box b)
        {
            switch (b.component)
            {
                case HierarchyNode _:
                    return new Color(0.45f, 0.92f, 0.55f);
                case HierarchyAggregator agg:
                    return agg.TryGetState(out _) ? new Color(1f, 0.85f, 0.35f) : new Color(0.55f, 0.58f, 0.62f);
                case HierarchyStateProvider leaf:
                    return leaf.IsActive ? new Color(0.45f, 0.92f, 0.55f) : new Color(0.55f, 0.58f, 0.62f);
                default:
                    return Color.white;
            }
        }

        private static void DrawBorder(Rect r, Color c, int thickness)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, thickness), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - thickness, r.width, thickness), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, thickness, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - thickness, r.y, thickness, r.height), c);
        }

        // ─── Live preview wiring ──────────────────────────────────────────

        private void SubscribeToAllProviders()
        {
            UnsubscribeFromAll();
            if (_target == null) return;

            CollectAllProviders(_target.transform, _subscribed);
            foreach (var p in _subscribed)
                p.onStateMayHaveChanged += OnProviderChanged;
        }

        private void UnsubscribeFromAll()
        {
            foreach (var p in _subscribed)
                p.onStateMayHaveChanged -= OnProviderChanged;
            _subscribed.Clear();
        }

        private static void CollectAllProviders(Transform root, HashSet<IHierarchyStateProvider> output)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (c.TryGetComponent<HierarchyNode>(out _)) continue;

                if (c.TryGetComponent<HierarchyAggregator>(out var agg)) output.Add(agg);
                if (c.TryGetComponent<HierarchyStateProvider>(out var leaf)) output.Add(leaf);

                CollectAllProviders(c, output);
            }
        }

        private void OnProviderChanged()
        {
            // Driver handles the modifier transition; window just repaints.
            Repaint();
        }
    }
}
