using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// GraphView that visualizes and edits a single <see cref="HierarchyNode"/>'s graph.
    /// Nodes correspond to the Out node and to each provider/aggregator on the host GO.
    /// Edges are derived from each source's <see cref="DexterityEdge"/> outputs.
    /// </summary>
    public class DexterityGraphView : GraphView
    {
        // Highlight colors for the currently-winning state-input port + its edges.
        internal static readonly Color s_activeColor = new Color(0.35f, 0.95f, 0.85f);
        internal static readonly Color s_inactiveColor = new Color(0.55f, 0.55f, 0.55f);

        private HierarchyNode _node;
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
            HierarchyPreviewOverrides.onChanged += RefreshActiveHighlight;
            this.RegisterCallback<DetachFromPanelEvent>(_ =>
                HierarchyPreviewOverrides.onChanged -= RefreshActiveHighlight);
        }

        public void RebuildGraph(HierarchyNode node)
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
            foreach (var p in node.GetComponents<HierarchyStateProvider>())
                AddSourceNode(p, isAggregator: false);
            foreach (var a in node.GetComponents<HierarchyAggregator>())
                AddSourceNode(a, isAggregator: true);

            // Lay out / load positions
            ApplyStoredPositions();

            // Build edges
            BuildEdges();

            // Apply initial active-port + edge highlight.
            RefreshActiveHighlight();
        }

        /// <summary>
        /// Highlight the currently-winning state-input port and the edges feeding it.
        /// At edit time, uses <see cref="HierarchyNode.EvaluateTreeEditor"/>.
        /// At runtime, uses <see cref="BaseStateNode.GetActiveState"/>.
        /// </summary>
        public void RefreshActiveHighlight()
        {
            if (_outNodeView == null) return;

            // Reset all out-node port + edge colors to inactive.
            foreach (var kv in _portStateName)
            {
                kv.Key.portColor = s_inactiveColor;
            }
            foreach (var e in edges.ToList())
            {
                e.edgeControl.inputColor = s_inactiveColor;
                e.edgeControl.outputColor = s_inactiveColor;
            }

            if (_node == null) return;

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
            if (string.IsNullOrEmpty(activeState)) return;

            var activePort = _outNodeView.GetInputPortForState(activeState);
            if (activePort == null) return;

            activePort.portColor = s_activeColor;
            foreach (var e in edges.ToList())
            {
                if (e.input == activePort)
                {
                    e.edgeControl.inputColor = s_activeColor;
                    e.edgeControl.outputColor = s_activeColor;
                }
            }
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
                    if (comp is HierarchyNode)
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
                if (src is HierarchyNode) continue;   // Out node doesn't have outputs

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

            var providerTypes = TypeCache.GetTypesDerivedFrom<HierarchyStateProvider>()
                .Where(t => !t.IsAbstract).OrderBy(t => t.Name).ToList();
            var aggregatorTypes = TypeCache.GetTypesDerivedFrom<HierarchyAggregator>()
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
                evt.menu.AppendAction("(select a GameObject with a HierarchyNode to enable)",
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

    /// <summary>Node view for the Out node (HierarchyNode itself).</summary>
    public class DexterityOutNodeView : Node
    {
        public HierarchyNode Node { get; }
        private readonly DexterityGraphView _view;
        private readonly List<Port> _inputPorts = new();
        private readonly Dictionary<string, Port> _portByState = new();

        public DexterityOutNodeView(HierarchyNode node, DexterityGraphView view)
        {
            Node = node;
            _view = view;
            title = "Out";
            titleContainer.style.backgroundColor = new Color(0.20f, 0.40f, 0.70f);

            // Embed the Out node's inspector (stateInputs + initialState).
            var imgui = new IMGUIContainer(() => DrawOutNodeInspector(node, view));
            imgui.style.minWidth = 240f;
            extensionContainer.Add(imgui);
            RefreshExpandedState();
            RefreshPorts();
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

        private static void DrawOutNodeInspector(HierarchyNode node, DexterityGraphView view)
        {
            var so = new SerializedObject(node);
            so.Update();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(so.FindProperty("initialState"), new GUIContent("Initial / fallback state"));
            EditorGUILayout.PropertyField(so.FindProperty("stateInputs"), new GUIContent("State inputs"), true);
            if (EditorGUI.EndChangeCheck())
            {
                so.ApplyModifiedProperties();
                // Rebuild ports + edges to reflect stateInputs changes.
                EditorApplication.delayCall += () => view.RebuildGraph(node);
            }
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

            // Periodic refresh — handles both override changes and real IsActive changes.
            // Also pokes the view to refresh active-port highlighting (catches changes
            // made directly to source fields without going through the override registry).
            _stateBtn.schedule.Execute(() => {
                RefreshOverrideUI();
                _view?.RefreshActiveHighlight();
            }).Every(150);

            // Embedded inspector (default inspector minus outputs + graphPosition + m_Script).
            var imgui = new IMGUIContainer(() => DrawSourceInspector(component));
            imgui.style.minWidth = 220f;
            extensionContainer.Add(imgui);
            RefreshExpandedState();
            RefreshPorts();
        }

        private void OnStateButtonClicked()
        {
            if (Component == null) return;
            var src = (IDexteritySource)Component;
            // Flip the current value. Auto-enables override (Set always overrides).
            bool current = HierarchyPreviewOverrides.TryGet(src, out var ov) ? ov : src.IsActive;
            HierarchyPreviewOverrides.Set(src, !current);
            RefreshOverrideUI();
        }

        private void OnOverrideToggled(ChangeEvent<bool> evt)
        {
            if (Component == null) return;
            var src = (IDexteritySource)Component;
            if (evt.newValue)
            {
                // Enable override, capturing current actual IsActive as the override value.
                HierarchyPreviewOverrides.Set(src, src.IsActive);
            }
            else
            {
                HierarchyPreviewOverrides.Clear(src);
            }
            RefreshOverrideUI();
        }

        private void RefreshOverrideUI()
        {
            if (Component == null || _stateBtn == null) return;
            var src = (IDexteritySource)Component;
            bool hasOv = HierarchyPreviewOverrides.TryGet(src, out var ov);
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

        private static void DrawSourceInspector(Component component)
        {
            if (component == null) return;
            var so = new SerializedObject(component);
            so.Update();

            EditorGUI.BeginChangeCheck();
            var prop = so.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (prop.name == "m_Script") continue;
                if (prop.name == "outputs") continue;
                if (prop.name == "graphPosition") continue;
                EditorGUILayout.PropertyField(prop, true);
            }
            if (EditorGUI.EndChangeCheck())
                so.ApplyModifiedProperties();
        }
    }
}
