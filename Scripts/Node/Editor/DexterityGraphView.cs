using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// GraphView that visualizes and edits a single <see cref="GraphNode"/>'s graph.
    ///
    /// <b>Data model.</b> Components on the host GameObject are the single source of
    /// truth — every <see cref="GraphStateProvider"/> / <see cref="GraphAggregator"/>
    /// + the <see cref="GraphNode"/> itself. Their serialized fields (<c>outputs</c>,
    /// <c>stateInputs</c>, <c>graphPosition</c>) define the graph. The view is a
    /// derived projection of those components — never mutated directly.
    ///
    /// <b>Mutation flow (visual → components):</b>
    /// <list type="bullet">
    /// <item>User-driven edge create/delete/move + node delete go through GraphView's
    ///       <c>OnGraphViewChanged</c> → <see cref="CommitEdgeCreation"/> / Removal /
    ///       <see cref="CommitMove"/> / <see cref="CommitSourceRemoval"/> — each writes
    ///       to the underlying <see cref="SerializedObject"/> + calls
    ///       <see cref="RequestRebuild"/>.</item>
    /// <item>Programmatic mutations (search-window add, paste, stateInputs edit) also
    ///       end in <see cref="RequestRebuild"/>.</item>
    /// </list>
    ///
    /// <b>Rebuild flow (components → visual):</b> <see cref="RequestRebuild"/> is the
    /// single entry point — it deduplicates within a frame (multiple mutations queue
    /// exactly one rebuild). External events (<c>Selection</c>, <c>Undo</c>,
    /// <c>hierarchyChanged</c>) feed in via the window's structural-diff check.
    ///
    /// No spaghetti: every "I changed something — please refresh" path lands in
    /// <see cref="RequestRebuild"/>; every "actually apply the change to the model"
    /// path runs through the GraphView-change callback above.
    /// </summary>
    public class DexterityGraphView : GraphView
    {
        // Highlight colors for the currently-winning state-input port + its edges.
        // Active uses the current node's mode color so the visual tracks Preview vs Live
        // for the specific GraphNode the graph is showing.
        internal Color ActiveColor => DexterityPreview.GetNodeColor(_node);
        internal static readonly Color s_inactiveColor = new Color(0.55f, 0.55f, 0.55f);

        private GraphNode _node;
        private readonly Dictionary<Component, Node> _nodeByComponent = new();
        private readonly Dictionary<Port, string> _portStateName = new();   // for Out node ports
        private DexterityOutNodeView _outNodeView;
        private bool _rebuilding;       // suppress GraphViewChange callback during programmatic clears
        private bool _rebuildPending;   // de-dup multiple RequestRebuild calls within a frame

        public DexterityGraphView()
        {
            SetupZoom(0.25f, 2f);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            // Light styling
            style.flexGrow = 1;
            style.backgroundColor = new Color(0.16f, 0.16f, 0.18f);

            graphViewChanged = OnGraphViewChanged;
            // Spacebar + the "Create Node" context-menu entry → opens our SearchWindow.
            nodeCreationRequest = OpenAddSourceSearchWindow;
            // Copy / Paste / Duplicate of source nodes.
            serializeGraphElements = SerializeForClipboard;
            unserializeAndPaste = (op, data) => PasteFromClipboard(data);
            canPasteSerializedData = data => data != null && data.StartsWith(kClipboardPrefix);
            this.RegisterCallback<KeyDownEvent>(OnKeyDown);

            // Refresh active-port highlight whenever overrides change.
            GraphPreviewOverrides.onChanged += RefreshActiveHighlight;
            this.RegisterCallback<DetachFromPanelEvent>(_ =>
                GraphPreviewOverrides.onChanged -= RefreshActiveHighlight);
        }

        /// <summary>
        /// Queue a graph rebuild after current event handling completes. Deduplicates —
        /// any number of calls within a frame coalesce into one <see cref="RebuildGraph"/>.
        /// All mutation sites in this file (and external callers like the Out-node
        /// inspector when stateInputs changes) should go through this method rather
        /// than scheduling their own <c>delayCall</c>.
        /// </summary>
        /// <param name="onAfterRebuild">Optional callback invoked once after the
        /// rebuild completes (after node/edge instances exist). Multiple callbacks
        /// across coalesced RequestRebuild() calls are all run in order.</param>
        internal void RequestRebuild(System.Action onAfterRebuild = null)
        {
            if (onAfterRebuild != null)
            {
                var prev = _onAfterNextRebuild;
                _onAfterNextRebuild = prev == null ? onAfterRebuild : () => { prev(); onAfterRebuild(); };
            }
            if (_rebuildPending) return;
            _rebuildPending = true;
            EditorApplication.delayCall += () =>
            {
                _rebuildPending = false;
                RebuildGraph(_node);
                var cb = _onAfterNextRebuild;
                _onAfterNextRebuild = null;
                cb?.Invoke();
            };
        }

        private System.Action _onAfterNextRebuild;

        public void RebuildGraph(GraphNode node)
        {
            _node = node;
            _rebuilding = true;
            try
            {
                // Clear current graph (programmatic; do NOT commit user-style deletions)
                DeleteElements(graphElements.ToList());
            }
            finally
            {
                _rebuilding = false;
            }
            _nodeByComponent.Clear();
            _portStateName.Clear();
            _outNodeView = null;

            if (node == null) return;

            // Build Out node (sources are discovered from host components below)
            _outNodeView = new DexterityOutNodeView(node, this);
            _outNodeView.RebuildPorts(_portStateName);
            AddElement(_outNodeView);
            _nodeByComponent[node] = _outNodeView;

            // Build source nodes
            foreach (var p in node.GetComponents<GraphStateProvider>())
                AddSourceNode(p, isAggregator: false);
            foreach (var a in node.GetComponents<GraphAggregator>())
                AddSourceNode(a, isAggregator: true);

            // Lay out / load positions
            ApplyStoredPositions();

            // Build edges
            BuildEdges();

            // Apply initial active-port + edge highlight.
            RefreshActiveHighlight();
        }

        /// <summary>
        /// Color ports as a pure projection of the data model. Edges derive their
        /// color from port colors (via <c>Edge.UpdateEdgeControl</c>), so port
        /// coloring is the stable channel — direct writes to <c>edgeControl</c>
        /// get reset on every layout pass.
        ///
        /// <para><b>Rule (one principle, applied recursively via topo eval):</b></para>
        /// <list type="bullet">
        ///   <item><b>Source ports (input + output) on a provider/aggregator</b> →
        ///         the owner's own <see cref="IDexteritySource.IsActive"/>. Aggregators
        ///         compute IsActive from their inputs (AND/OR/etc.), so port color
        ///         chains naturally through <c>GraphNode.EvaluateSources</c>'s topo
        ///         order — no separate "any incoming active" heuristic.</item>
        ///   <item><b>Out-node input ports</b> → "any connected source active" (raw
        ///         signal indicator). Out has no IsActive of its own; each port is
        ///         a state-input slot, and we want to see "the Press signal is on"
        ///         even when a higher-priority state masks it.</item>
        /// </list>
        ///
        /// <para>Edge between two cyan ports renders fully cyan. Cyan→gray gradient
        /// (active source feeding a not-yet-firing aggregator) is the right visual:
        /// "signal entering but not propagating through".</para>
        /// </summary>
        public void RefreshActiveHighlight()
        {
            if (_outNodeView == null) return;

            if (_node == null)
            {
                foreach (var kv in _portStateName) kv.Key.portColor = s_inactiveColor;
                return;
            }

            // Drive evaluation so every source's IsActive (and aggregator-cached
            // outputs) reflect the current pass. Topo order inside EvaluateSources
            // guarantees aggregator results are correct when we read them below.
            if (Application.isPlaying && _node.initialized)
                _node.GetActiveState();
            else
                _node.EvaluateTreeEditor();

            var activeColor = ActiveColor;
            var inactiveColor = s_inactiveColor;

            // Source nodes: every port (in + out) reflects the source's own IsActive.
            // Aggregator IsActive already accounts for its incoming logic, so this
            // gives us the chain the user wants: A→B→C edges light up one step at a
            // time as each node's IsActive flips true.
            foreach (var kv in _nodeByComponent)
            {
                if (kv.Value is DexteritySourceNodeView srcView && srcView.Component != null)
                {
                    var color = ((IDexteritySource)srcView.Component).IsActive
                        ? activeColor : inactiveColor;
                    srcView.OutputPort.portColor = color;
                    if (srcView.IsAggregator && srcView.InputPort != null)
                        srcView.InputPort.portColor = color;
                }
            }

            // Out-node state-input ports: "any source feeding is active". Out is the
            // sink — no IsActive of its own; each port surfaces the raw signal for
            // its named state (visible even when priority-masked).
            foreach (var kv in _portStateName)
                kv.Key.portColor = AnyIncomingActive(kv.Key) ? activeColor : inactiveColor;

            // Port-color changes don't auto-refresh edges — push the new colors now.
            foreach (var e in edges.ToList())
                e.UpdateEdgeControl();
        }

        private static bool AnyIncomingActive(Port port)
        {
            foreach (var e in port.connections)
            {
                if (e.output?.node is DexteritySourceNodeView src
                    && src.Component != null
                    && ((IDexteritySource)src.Component).IsActive)
                    return true;
            }
            return false;
        }

        private void AddSourceNode(Component src, bool isAggregator)
        {
            var view = new DexteritySourceNodeView(src, this, isAggregator);
            AddElement(view);
            _nodeByComponent[src] = view;
        }

        private void ApplyStoredPositions()
        {
            const float defaultX = 100f;
            const float defaultY = 100f;
            int autoLayoutIdx = 0;
            foreach (var kv in _nodeByComponent)
            {
                var comp = kv.Key;
                var nodeView = kv.Value;
                var so = new SerializedObject(comp);
                var posProp = so.FindProperty("graphPosition");
                Vector2 pos;
                if (posProp != null && posProp.vector2Value != Vector2.zero)
                {
                    pos = posProp.vector2Value;
                }
                else
                {
                    // Auto-place: Out on the right, sources on the left in a column.
                    if (comp is GraphNode)
                        pos = new Vector2(500f, 100f);
                    else
                        pos = new Vector2(defaultX, defaultY + autoLayoutIdx++ * 140f);
                }
                nodeView.SetPosition(new Rect(pos, new Vector2(260, 120)));
            }
        }

        private void BuildEdges()
        {
            if (_node == null) return;

            foreach (var kv in _nodeByComponent)
            {
                var src = kv.Key;
                if (src is GraphNode) continue;   // Out node doesn't have outputs

                var so = new SerializedObject(src);
                var outsProp = so.FindProperty("outputs");
                if (outsProp == null || !outsProp.isArray) continue;

                var srcView = kv.Value as DexteritySourceNodeView;
                if (srcView == null) continue;

                for (var i = 0; i < outsProp.arraySize; i++)
                {
                    var edgeProp = outsProp.GetArrayElementAtIndex(i);
                    var target = edgeProp.FindPropertyRelative("target").objectReferenceValue as Component;
                    var port = edgeProp.FindPropertyRelative("targetPort").stringValue;
                    if (target == null) continue;
                    if (!_nodeByComponent.TryGetValue(target, out var targetNode)) continue;

                    Port targetPort = null;
                    if (targetNode is DexterityOutNodeView outView)
                        targetPort = outView.GetInputPortForState(port);
                    else if (targetNode is DexteritySourceNodeView aggView && aggView.IsAggregator)
                        targetPort = aggView.InputPort;

                    if (targetPort == null) continue;
                    var edge = srcView.OutputPort.ConnectTo(targetPort);
                    AddElement(edge);
                }
            }
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compat = new List<Port>();
            ports.ForEach(p =>
            {
                if (p == startPort) return;
                if (p.node == startPort.node) return;
                if (p.direction == startPort.direction) return;
                compat.Add(p);
            });
            return compat;
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (_rebuilding) return change;   // suppress commits during programmatic clear

            // Group every commit inside this dispatch into a single Undo entry — a
            // multi-select drag, a multi-edge delete, an edge-creation-plus-move,
            // etc., all collapse to one Ctrl+Z step.
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            string groupName = null;

            if (change.edgesToCreate != null && change.edgesToCreate.Count > 0)
            {
                foreach (var edge in change.edgesToCreate)
                    CommitEdgeCreation(edge);
                groupName = change.edgesToCreate.Count == 1 ? "Create Edge" : "Create Edges";
            }
            if (change.elementsToRemove != null && change.elementsToRemove.Count > 0)
            {
                int edges = 0, srcs = 0;
                foreach (var elem in change.elementsToRemove)
                {
                    if (elem is Edge edge) { CommitEdgeRemoval(edge); edges++; }
                    else if (elem is DexteritySourceNodeView snv) { CommitSourceRemoval(snv); srcs++; }
                }
                if (srcs > 0)      groupName = srcs == 1 ? "Delete Graph Source" : "Delete Graph Sources";
                else if (edges > 0) groupName = edges == 1 ? "Delete Edge" : "Delete Edges";
            }
            if (change.movedElements != null && change.movedElements.Count > 0)
            {
                foreach (var elem in change.movedElements)
                    if (elem is Node n) CommitMove(n);
                groupName ??= change.movedElements.Count == 1 ? "Move Graph Node" : "Move Graph Nodes";
            }

            if (groupName != null)
            {
                Undo.SetCurrentGroupName(groupName);
                Undo.CollapseUndoOperations(group);
            }
            return change;
        }

        private void CommitEdgeCreation(Edge edge)
        {
            // edge.output is on a source (provider/aggregator); edge.input is on Out or an aggregator
            if (edge.output?.node is not DexteritySourceNodeView srcView) return;
            var src = srcView.Component;
            if (src == null) return;

            string portName = "";
            Component targetComp = null;

            if (edge.input?.node is DexterityOutNodeView outView)
            {
                targetComp = outView.Node;
                portName = _portStateName.TryGetValue(edge.input, out var n) ? n : "";
            }
            else if (edge.input?.node is DexteritySourceNodeView aggView && aggView.IsAggregator)
            {
                targetComp = aggView.Component;
                portName = "";
            }

            if (targetComp == null) return;

            var so = new SerializedObject(src);
            var outsProp = so.FindProperty("outputs");
            outsProp.arraySize++;
            var newEdge = outsProp.GetArrayElementAtIndex(outsProp.arraySize - 1);
            newEdge.FindPropertyRelative("target").objectReferenceValue = targetComp;
            newEdge.FindPropertyRelative("targetPort").stringValue = portName;
            so.ApplyModifiedProperties();
        }

        private void CommitEdgeRemoval(Edge edge)
        {
            if (edge.output?.node is not DexteritySourceNodeView srcView) return;
            var src = srcView.Component;
            if (src == null) return;

            Component targetComp = null;
            string portName = "";
            if (edge.input?.node is DexterityOutNodeView outView)
            {
                targetComp = outView.Node;
                portName = _portStateName.TryGetValue(edge.input, out var n) ? n : "";
            }
            else if (edge.input?.node is DexteritySourceNodeView aggView)
            {
                targetComp = aggView.Component;
                portName = "";
            }
            if (targetComp == null) return;

            var so = new SerializedObject(src);
            var outsProp = so.FindProperty("outputs");
            for (var i = 0; i < outsProp.arraySize; i++)
            {
                var e = outsProp.GetArrayElementAtIndex(i);
                var t = e.FindPropertyRelative("target").objectReferenceValue as Component;
                var p = e.FindPropertyRelative("targetPort").stringValue;
                if (t == targetComp && p == portName)
                {
                    outsProp.DeleteArrayElementAtIndex(i);
                    so.ApplyModifiedProperties();
                    return;
                }
            }
        }

        private void CommitSourceRemoval(DexteritySourceNodeView snv)
        {
            if (snv.Component == null) return;
            Undo.DestroyObjectImmediate(snv.Component);
            RequestRebuild();
        }

        private void CommitMove(Node n)
        {
            Component comp = n switch
            {
                DexterityOutNodeView outView => outView.Node,
                DexteritySourceNodeView snv => snv.Component,
                _ => null,
            };
            if (comp == null) return;

            var so = new SerializedObject(comp);
            var posProp = so.FindProperty("graphPosition");
            if (posProp == null) return;
            posProp.vector2Value = n.GetPosition().position;
            so.ApplyModifiedProperties();
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            // Single entry that opens our SearchWindow picker (same as Spacebar).
            // Base GraphView's contextual menu adds its own "Create Node" + cut/copy/paste/
            // delete entries via nodeCreationRequest + the serialize/unserialize delegates
            // we wired in the constructor, so we don't need to duplicate them here.
            base.BuildContextualMenu(evt);
        }

        private void OpenAddSourceSearchWindow(NodeCreationContext ctx)
        {
            if (_node == null) return;
            var provider = ScriptableObject.CreateInstance<DexterityAddSourceSearchProvider>();
            provider.view = this;

            // SearchWindowContext gives a screen-space cursor; the previous chain
            // (this.WorldToLocal then contentViewContainer.WorldToLocal) double-
            // converted, treating screen-space as panel-world and dropping the
            // window's screen origin entirely. The standard GraphView recipe is
            // screen → window-local → contentViewContainer-local, which is what
            // contentViewContainer.WorldToLocal expects (it applies pan/zoom).
            // The result is then shifted by half the node size so the cursor lands
            // near the node center instead of its top-left corner.
            var screenMouse = ctx.screenMousePosition;
            var hostWindow = EditorWindow.focusedWindow;
            var windowMouse = hostWindow != null
                ? screenMouse - hostWindow.position.position
                : screenMouse;
            var graphLocal = contentViewContainer.WorldToLocal(windowMouse);
            provider.spawnGraphPos = graphLocal - new Vector2(130f, 60f);

            SearchWindow.Open(new SearchWindowContext(screenMouse), provider);
        }

        // ---- Copy / Paste / Duplicate ---------------------------------------
        private const string kClipboardPrefix = "DEX_GRAPH:";

        private string SerializeForClipboard(IEnumerable<GraphElement> elements)
        {
            // Only source nodes (providers/aggregators) are copyable. The Out node
            // is a singleton per host and edges are derived from source outputs.
            var sb = new System.Text.StringBuilder();
            sb.Append(kClipboardPrefix);
            bool first = true;
            foreach (var e in elements)
            {
                if (e is not DexteritySourceNodeView snv || snv.Component == null) continue;
                if (!first) sb.Append("\n---\n");
                first = false;
                sb.Append(snv.Component.GetType().AssemblyQualifiedName);
                sb.Append("\n");
                sb.Append(EditorJsonUtility.ToJson(snv.Component));
            }
            return sb.ToString();
        }

        private void PasteFromClipboard(string data)
        {
            if (_node == null || data == null || !data.StartsWith(kClipboardPrefix)) return;
            var body = data.Substring(kClipboardPrefix.Length);
            var pasted = new List<Component>();

            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            foreach (var entry in body.Split(new[] { "\n---\n" }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                var splitIdx = entry.IndexOf('\n');
                if (splitIdx < 0) continue;
                var typeName = entry.Substring(0, splitIdx);
                var json = entry.Substring(splitIdx + 1);
                var type = System.Type.GetType(typeName);
                if (type == null) continue;
                var newComp = Undo.AddComponent(_node.gameObject, type);
                if (newComp == null) continue;
                // FromJsonOverwrite bypasses the SerializedProperty path so Unity
                // doesn't see it as a tracked change. RecordObject before snapshots
                // the default state — undo restores it, redo replays the paste.
                Undo.RecordObject(newComp, "Paste");
                EditorJsonUtility.FromJsonOverwrite(json, newComp);
                EditorUtility.SetDirty(newComp);
                // Nudge position so the paste doesn't sit exactly on top of the original.
                var so = new SerializedObject(newComp);
                var posProp = so.FindProperty("graphPosition");
                if (posProp != null)
                {
                    posProp.vector2Value = posProp.vector2Value + new Vector2(30, 30);
                    so.ApplyModifiedProperties();
                }
                pasted.Add(newComp);
            }
            Undo.SetCurrentGroupName(pasted.Count == 1 ? "Paste Graph Source" : "Paste Graph Sources");
            Undo.CollapseUndoOperations(group);
            // Re-select pasted components in the graph view for immediate continued editing.
            RequestRebuild(onAfterRebuild: () =>
            {
                ClearSelection();
                foreach (var c in pasted)
                {
                    if (_nodeByComponent.TryGetValue(c, out var view))
                        AddToSelection(view);
                }
            });
        }

        private void AddSourceOfType(System.Type type) => AddSourceOfTypeAt(type, null);

        internal void AddSourceOfTypeAt(System.Type type, Vector2? graphPos)
        {
            if (_node == null) return;

            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            var comp = Undo.AddComponent(_node.gameObject, type);
            if (comp != null && graphPos.HasValue)
            {
                var so = new SerializedObject(comp);
                var posProp = so.FindProperty("graphPosition");
                if (posProp != null)
                {
                    posProp.vector2Value = graphPos.Value;
                    so.ApplyModifiedProperties();
                }
            }
            Undo.SetCurrentGroupName($"Add {type.Name}");
            Undo.CollapseUndoOperations(group);

            RequestRebuild();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace)
            {
                DeleteSelection();
                evt.StopPropagation();
            }
        }

        internal static string StripSuffix(string name, string suffix)
        {
            if (name.EndsWith(suffix) && name.Length > suffix.Length)
                return name.Substring(0, name.Length - suffix.Length);
            return name;
        }
    }

    // ---------------------------------------------------------------------------
    // Node views
    // ---------------------------------------------------------------------------

    /// <summary>Node view for the Out node (GraphNode itself).</summary>
    public class DexterityOutNodeView : Node
    {
        public GraphNode Node { get; }
        private readonly DexterityGraphView _view;
        private readonly List<Port> _inputPorts = new();
        private readonly Dictionary<string, Port> _portByState = new();

        public DexterityOutNodeView(GraphNode node, DexterityGraphView view)
        {
            Node = node;
            _view = view;
            title = "Out";
            ApplyModeTint();
            DexterityPreview.onChanged += ApplyModeTint;
            this.RegisterCallback<DetachFromPanelEvent>(_ => DexterityPreview.onChanged -= ApplyModeTint);

            // Embed the Out node's inspector via UIElements PropertyField — event-driven
            // (redraws only when the bound serialized property actually changes), unlike
            // IMGUIContainer which redraws on every UIElements layout pass.
            var so = new SerializedObject(node);
            var body = new VisualElement { style = { minWidth = 240f } };

            var initialStateField = new PropertyField(so.FindProperty("initialState"),
                "Initial / fallback state");
            body.Add(initialStateField);

            // stateInputs is edited in the GraphNode inspector — embedding the list
            // here caused the whole graph to rebuild on every keystroke (each change
            // event destroyed the text field and stole focus).

            body.Bind(so);
            extensionContainer.Add(body);
            RefreshExpandedState();
            RefreshPorts();
        }

        private void ApplyModeTint()
        {
            // Out node title bar reflects this node's current mode (None/Preview/Live)
            // so the user always knows whether they're looking at a live runtime state
            // or an edit-time preview — even with multiple windows showing different modes.
            titleContainer.style.backgroundColor = DexterityPreview.GetNodeColor(Node);
        }

        public Port GetInputPortForState(string stateName)
        {
            if (string.IsNullOrEmpty(stateName)) return null;
            _portByState.TryGetValue(stateName, out var p);
            return p;
        }

        public void RebuildPorts(Dictionary<Port, string> portStateNameMap)
        {
            foreach (var p in _inputPorts) inputContainer.Remove(p);
            _inputPorts.Clear();
            _portByState.Clear();

            var so = new SerializedObject(Node);
            var inputs = so.FindProperty("stateInputs");
            if (inputs == null || !inputs.isArray) { RefreshPorts(); return; }

            for (var i = 0; i < inputs.arraySize; i++)
            {
                var stateName = inputs.GetArrayElementAtIndex(i).stringValue;
                if (string.IsNullOrEmpty(stateName)) continue;
                var port = InstantiatePort(Orientation.Horizontal, Direction.Input,
                    Port.Capacity.Multi, typeof(bool));
                port.portName = stateName;
                inputContainer.Add(port);
                _inputPorts.Add(port);
                _portByState[stateName] = port;
                portStateNameMap[port] = stateName;
            }

            RefreshPorts();
            RefreshExpandedState();
        }

    }

    /// <summary>Node view for a provider or aggregator source.</summary>
    public class DexteritySourceNodeView : Node
    {
        public Component Component { get; }
        public bool IsAggregator { get; }
        public Port OutputPort { get; private set; }
        public Port InputPort { get; private set; }   // aggregators only

        private readonly DexterityGraphView _view;
        private Button _stateBtn;
        private Toggle _overrideToggle;

        public DexteritySourceNodeView(Component component, DexterityGraphView view, bool isAggregator)
        {
            Component = component;
            IsAggregator = isAggregator;
            _view = view;
            title = DexterityGraphView.StripSuffix(component.GetType().Name,
                isAggregator ? "Aggregator" : "Provider");
            titleContainer.style.backgroundColor = isAggregator
                ? new Color(0.70f, 0.55f, 0.15f)   // amber
                : new Color(0.20f, 0.55f, 0.20f);  // green

            if (isAggregator)
            {
                InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input,
                    Port.Capacity.Multi, typeof(bool));
                InputPort.portName = "in";
                inputContainer.Add(InputPort);
            }

            OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output,
                Port.Capacity.Multi, typeof(bool));
            OutputPort.portName = "out";
            outputContainer.Add(OutputPort);

            // Binary ON/OFF toggle (colored) + small "override" checkbox.
            // Clicking ON/OFF auto-enables override. Unchecking override clears it.
            _stateBtn = new Button(OnStateButtonClicked) { text = "OFF" };
            _stateBtn.style.width = 44f;
            _stateBtn.style.marginLeft = 4f;
            _stateBtn.style.marginRight = 0f;
            _stateBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            _stateBtn.tooltip = "Override IsActive. Click to flip; auto-enables override.";
            titleContainer.Add(_stateBtn);

            _overrideToggle = new Toggle { value = false };
            _overrideToggle.style.marginLeft = 2f;
            _overrideToggle.style.marginRight = 4f;
            _overrideToggle.tooltip = "Override active";
            _overrideToggle.RegisterValueChangedCallback(OnOverrideToggled);
            titleContainer.Add(_overrideToggle);

            // Event-driven refresh — fires on:
            //   • override Set/Clear (NotifyExternalChanged → onStateMayHaveChanged)
            //   • the source's own state change (subclass MarkChanged)
            //   • OnValidate from inspector field edits (the base class fires onStateMayHaveChanged)
            // Replaces the previous 150ms polling, which was running on every source
            // node regardless of activity (visible flicker in big graphs).
            var src = (IDexteritySource)component;
            _onSourceChanged = () =>
            {
                RefreshOverrideUI();
                _view?.RefreshActiveHighlight();
            };
            src.onStateMayHaveChanged += _onSourceChanged;
            // Also catch global ClearAll (which doesn't fire per-source events).
            GraphPreviewOverrides.onChanged += _onSourceChanged;
            this.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                if (Component != null)
                    ((IDexteritySource)Component).onStateMayHaveChanged -= _onSourceChanged;
                GraphPreviewOverrides.onChanged -= _onSourceChanged;
            });

            // Embedded inspector via UIElements PropertyField — event-driven, redraws
            // only when bound props change. Crucially: during Modifier transitions
            // (which mutate OTHER components, not this provider), this body sits idle.
            var so = new SerializedObject(component);
            var body = new VisualElement { style = { minWidth = 220f } };
            var iter = so.GetIterator();
            bool enterChildren = true;
            while (iter.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iter.name == "m_Script") continue;
                if (iter.name == "outputs") continue;
                if (iter.name == "graphPosition") continue;
                body.Add(new PropertyField(iter.Copy()));
            }
            body.Bind(so);
            extensionContainer.Add(body);

            RefreshOverrideUI();
            RefreshExpandedState();
            RefreshPorts();
        }

        private System.Action _onSourceChanged;

        private void OnStateButtonClicked()
        {
            if (Component == null) return;
            var src = (IDexteritySource)Component;
            // Flip the current value. Auto-enables override (Set always overrides).
            bool current = GraphPreviewOverrides.TryGet(src, out var ov) ? ov : src.IsActive;
            GraphPreviewOverrides.Set(src, !current);
            RefreshOverrideUI();
        }

        private void OnOverrideToggled(ChangeEvent<bool> evt)
        {
            if (Component == null) return;
            var src = (IDexteritySource)Component;
            if (evt.newValue)
            {
                // Enable override, capturing current actual IsActive as the override value.
                GraphPreviewOverrides.Set(src, src.IsActive);
            }
            else
            {
                GraphPreviewOverrides.Clear(src);
            }
            RefreshOverrideUI();
        }

        private void RefreshOverrideUI()
        {
            if (Component == null || _stateBtn == null) return;
            var src = (IDexteritySource)Component;
            bool hasOv = GraphPreviewOverrides.TryGet(src, out var ov);
            bool current = hasOv ? ov : src.IsActive;

            _stateBtn.text = current ? "ON" : "OFF";
            _stateBtn.style.backgroundColor = current
                ? new Color(0.18f, 0.62f, 0.22f)   // green
                : new Color(0.62f, 0.22f, 0.18f);  // red
            _stateBtn.style.color = Color.white;
            // Dimmed when reflecting actual (non-overridden) state.
            _stateBtn.style.opacity = hasOv ? 1f : 0.55f;

            if (_overrideToggle.value != hasOv)
                _overrideToggle.SetValueWithoutNotify(hasOv);
        }

    }
}
