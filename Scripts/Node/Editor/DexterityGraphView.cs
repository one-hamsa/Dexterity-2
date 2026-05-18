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
    /// Nodes correspond to the Out node and to each provider/aggregator on the host GO.
    /// Edges are derived from each source's <see cref="DexterityEdge"/> outputs.
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
        private bool _rebuilding;   // suppress GraphViewChange callback during programmatic clears

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
            // Note: we deliberately do NOT set nodeCreationRequest. The default
            // "Create Node" entry in the contextual menu would fire it and route
            // through a SearchWindow we don't provide. Instead, BuildContextualMenu
            // below adds explicit "Add Provider/X" + "Add Aggregator/X" entries.
            this.RegisterCallback<KeyDownEvent>(OnKeyDown);

            // Refresh active-port highlight whenever overrides change.
            GraphPreviewOverrides.onChanged += RefreshActiveHighlight;
            this.RegisterCallback<DetachFromPanelEvent>(_ =>
                GraphPreviewOverrides.onChanged -= RefreshActiveHighlight);
        }

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
        /// Highlight the currently-winning state-input port + every active edge.
        /// An edge is active when its source's <see cref="IDexteritySource.IsActive"/>
        /// is true. The node's <c>EvaluateTreeEditor()</c> call below ensures every
        /// source (including aggregators via their `_cachedOutput`) reflects the
        /// current pass — so no extra compute is needed to color edges, just a read.
        /// </summary>
        public void RefreshActiveHighlight()
        {
            if (_outNodeView == null) return;

            // Reset all out-node port colors. (Edge colors are set per-edge below.)
            foreach (var kv in _portStateName)
                kv.Key.portColor = s_inactiveColor;

            if (_node == null) return;

            // Drive a full evaluation so IsActive on every source is fresh.
            // (At runtime, GetActiveState() reflects whatever the Manager last
            //  evaluated; aggregator `_cachedOutput` lags by at most one frame.)
            string activeState = null;
            if (Application.isPlaying && _node.initialized)
            {
                var id = _node.GetActiveState();
                if (id != -1) activeState = Database.instance.GetStateAsString(id);
            }
            else
            {
                activeState = _node.EvaluateTreeEditor() ?? _node.initialState;
            }

            // Every edge: cyan if its source is active, gray otherwise. Covers
            // provider→Out, provider→aggregator, and aggregator→Out/aggregator
            // uniformly — they all carry a DexteritySourceNodeView on the output side.
            foreach (var e in edges.ToList())
            {
                bool sourceActive = e.output?.node is DexteritySourceNodeView srcView
                                    && srcView.Component != null
                                    && ((IDexteritySource)srcView.Component).IsActive;
                var color = sourceActive ? ActiveColor : s_inactiveColor;
                e.edgeControl.inputColor = color;
                e.edgeControl.outputColor = color;
            }

            // Highlight the winning state-input port (priority-respecting).
            if (string.IsNullOrEmpty(activeState)) return;
            var activePort = _outNodeView.GetInputPortForState(activeState);
            if (activePort != null) activePort.portColor = ActiveColor;
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

            if (change.edgesToCreate != null)
            {
                foreach (var edge in change.edgesToCreate)
                    CommitEdgeCreation(edge);
            }
            if (change.elementsToRemove != null)
            {
                foreach (var elem in change.elementsToRemove)
                {
                    if (elem is Edge edge)
                        CommitEdgeRemoval(edge);
                    else if (elem is DexteritySourceNodeView snv)
                        CommitSourceRemoval(snv);
                }
            }
            if (change.movedElements != null)
            {
                foreach (var elem in change.movedElements)
                {
                    if (elem is Node n) CommitMove(n);
                }
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
            EditorApplication.delayCall += () => RebuildGraph(_node);
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
            // Skip base.BuildContextualMenu — its "Create Node" entry routes to
            // nodeCreationRequest (unset) and does nothing. We provide a clear
            // reflection-based picker instead.
            var status = _node != null
                ? DropdownMenuAction.Status.Normal
                : DropdownMenuAction.Status.Disabled;

            var providerTypes = TypeCache.GetTypesDerivedFrom<GraphStateProvider>()
                .Where(t => !t.IsAbstract).OrderBy(t => t.Name).ToList();
            var aggregatorTypes = TypeCache.GetTypesDerivedFrom<GraphAggregator>()
                .Where(t => !t.IsAbstract).OrderBy(t => t.Name).ToList();

            foreach (var t in providerTypes)
            {
                var type = t;
                evt.menu.AppendAction($"Add Provider/{StripSuffix(type.Name, "Provider")}",
                    _ => AddSourceOfType(type), status);
            }
            foreach (var t in aggregatorTypes)
            {
                var type = t;
                evt.menu.AppendAction($"Add Aggregator/{StripSuffix(type.Name, "Aggregator")}",
                    _ => AddSourceOfType(type), status);
            }

            if (_node == null)
            {
                evt.menu.AppendSeparator();
                evt.menu.AppendAction("(select a GameObject with a GraphNode to enable)",
                    null, DropdownMenuAction.Status.Disabled);
            }
        }

        private void AddSourceOfType(System.Type type)
        {
            if (_node == null) return;
            Undo.AddComponent(_node.gameObject, type);
            EditorApplication.delayCall += () => RebuildGraph(_node);
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

            var stateInputsField = new PropertyField(so.FindProperty("stateInputs"), "State inputs");
            body.Add(stateInputsField);

            // When stateInputs changes (add/remove/rename a port), rebuild ports + edges.
            stateInputsField.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
                EditorApplication.delayCall += () => view.RebuildGraph(node));

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
