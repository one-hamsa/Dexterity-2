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
            nodeCreationRequest = ctx => ShowNodeCreationMenu(ctx);
            this.RegisterCallback<KeyDownEvent>(OnKeyDown);
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
            base.BuildContextualMenu(evt);
            if (_node == null) return;

            var providerTypes = TypeCache.GetTypesDerivedFrom<HierarchyStateProvider>()
                .Where(t => !t.IsAbstract).OrderBy(t => t.Name).ToList();
            var aggregatorTypes = TypeCache.GetTypesDerivedFrom<HierarchyAggregator>()
                .Where(t => !t.IsAbstract).OrderBy(t => t.Name).ToList();

            foreach (var t in providerTypes)
            {
                var type = t;
                evt.menu.AppendAction($"Add Provider/{type.Name}",
                    _ => AddSourceOfType(type),
                    DropdownMenuAction.Status.Normal);
            }
            evt.menu.AppendSeparator("Add Provider/");
            foreach (var t in aggregatorTypes)
            {
                var type = t;
                evt.menu.AppendAction($"Add Aggregator/{type.Name}",
                    _ => AddSourceOfType(type),
                    DropdownMenuAction.Status.Normal);
            }
        }

        private void ShowNodeCreationMenu(NodeCreationContext ctx)
        {
            // Spacebar / two-finger-tap entry point — same menu as right-click, just centered.
            var menu = new GenericMenu();
            foreach (var t in TypeCache.GetTypesDerivedFrom<HierarchyStateProvider>())
            {
                if (t.IsAbstract) continue;
                var type = t;
                menu.AddItem(new GUIContent($"Provider/{type.Name}"), false, () => AddSourceOfType(type));
            }
            foreach (var t in TypeCache.GetTypesDerivedFrom<HierarchyAggregator>())
            {
                if (t.IsAbstract) continue;
                var type = t;
                menu.AddItem(new GUIContent($"Aggregator/{type.Name}"), false, () => AddSourceOfType(type));
            }
            menu.ShowAsContext();
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

        public DexteritySourceNodeView(Component component, DexterityGraphView view, bool isAggregator)
        {
            Component = component;
            IsAggregator = isAggregator;
            title = component.GetType().Name;
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

            // Override pill (— / ON / OFF)
            var overrideBtn = new Button(() => CycleOverride()) { text = "—" };
            overrideBtn.style.width = 36f;
            titleContainer.Add(overrideBtn);
            // Refresh pill label each layout pass.
            overrideBtn.schedule.Execute(() =>
            {
                if (Component == null) return;
                var src = (IDexteritySource)Component;
                overrideBtn.text = HierarchyPreviewOverrides.TryGet(src, out var ov)
                    ? (ov ? "ON" : "OFF") : "—";
            }).Every(200);

            // Embedded inspector (default inspector minus outputs + graphPosition + m_Script).
            var imgui = new IMGUIContainer(() => DrawSourceInspector(component));
            imgui.style.minWidth = 220f;
            extensionContainer.Add(imgui);
            RefreshExpandedState();
            RefreshPorts();
        }

        private void CycleOverride()
        {
            if (Component == null) return;
            var src = (IDexteritySource)Component;
            if (!HierarchyPreviewOverrides.TryGet(src, out var ov))
                HierarchyPreviewOverrides.Set(src, true);
            else if (ov)
                HierarchyPreviewOverrides.Set(src, false);
            else
                HierarchyPreviewOverrides.Clear(src);
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
