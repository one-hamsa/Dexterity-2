using QuikGraph;
using QuikGraph.Graphviz;
using Rubjerg.Graphviz;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using UnityGraphNode = UnityEditor.Experimental.GraphView.Node;
using VisualNode = OneHamsa.Dexterity.Visual.Node;

namespace OneHamsa.Dexterity.Visual
{
    public class DebugGraphView : GraphView
    {
        public bool shouldUpdate = true;
        public float lastUpdateTime = -1;
        public float updateTimeDelta = 1f;
        Graph graph => Manager.Instance.graph;

        public DebugGraphView(DebugWindow editorWindow)
        {
            styleSheets.Add(Resources.Load<StyleSheet>("DebugGraph"));
            SetupZoom(ContentZoomer.DefaultMinScale * 2, ContentZoomer.DefaultMaxScale * 2);

            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new FreehandSelector());
        }

        Dictionary<VisualNode, GraphViewVisualNode> shownVisualNodes 
            = new Dictionary<VisualNode, GraphViewVisualNode>();
        Dictionary<BaseField, GraphViewFieldNode> shownFieldNodes 
            = new Dictionary<BaseField, GraphViewFieldNode>();
        Dictionary<VisualNode.OutputField, (Port, GraphViewVisualNode)> shownOutputFields
            = new Dictionary<VisualNode.OutputField, (Port, GraphViewVisualNode)>();

        void Sort()
        {
            // TODO check out ClusteredAdjacencyGraph once you have UnionFind
            var dgraph = new AdjacencyGraph<UnityGraphNode, Edge<UnityGraphNode>>();
            dgraph.AddVertexRange(nodes.ToList());
            dgraph.AddEdgeRange(edges.ToList().Select(e => new Edge<UnityGraphNode>(
                e.output.node,
                e.input.node
            )));

            var algo = new GraphvizAlgorithm<UnityGraphNode, Edge<UnityGraphNode>>(dgraph) 
            { ImageType = QuikGraph.Graphviz.Dot.GraphvizImageType.PlainText };
            algo.GraphFormat.RankDirection = QuikGraph.Graphviz.Dot.GraphvizRankDirection.LR;
            var root = RootGraph.FromDotString(algo.Generate());
            // Let's have graphviz compute a dot layout for us
            root.ComputeLayout();

            var nodes_ = nodes.ToList();
            var lnodes = root.Nodes().ToList();
            for (var i = 0; i < lnodes.Count(); ++i)
            {
                var pos = lnodes[i].Position();
                nodes_[i].SetPosition(new Rect(pos.X * 2, pos.Y * 2, 150, 150));
            }
        }

        public void Update()
        {
            if (!shouldUpdate)
                return;

            UpdateValues();

            if (Time.time - updateTimeDelta < lastUpdateTime)
                return;

            var visualNodes = UnityEngine.Object.FindObjectsOfType<VisualNode>();
            if (lastUpdateTime < graph.lastSuccessfulUpdate)
                RefreshVisualNodes(visualNodes);

            var activeNode = UnityEditor.Selection.activeGameObject?.GetComponent<VisualNode>();
            foreach (var visualNode in visualNodes)
            {
                var node = shownVisualNodes[visualNode];
                var isActive = activeNode == visualNode;
                if (isActive)
                {
                    RefreshFieldNodes(node.GetPosition(),
                        // TODO toggle
                        //GetRelatedFields(visualNode));
                        graph.nodes);
                }
                node.EnableInClassList("selected", isActive);
                node.BringToFront();
            }

            // TODO mutate
            foreach (var edge in edges.ToList())
            {
                RemoveElement(edge);
            }

            shownOutputFields.Clear();
            foreach (var node in shownVisualNodes.Values)
            {
                foreach (var input in node.outputs)
                {
                    var (ingoing, outgoing, label) = input.Value;
                    shownOutputFields.Add(input.Key, (outgoing, node));

                    var outputField = input.Key;
                    var port = input.Value.Item1;
                    port.DisconnectAll();

                    // connect the output field port to the field(s) that feeds it
                    foreach (var connection in graph.edges[outputField])
                    {
                        if (shownFieldNodes.TryGetValue(connection, out var outField))
                        {
                            var edge = port.ConnectTo(outField.outputPort);
                            AddElement(edge);
                            edge.BringToFront();
                        }
                    }
                }
                node.RefreshPorts();
            }

            foreach (var node in shownFieldNodes.Values)
            {
                node.inputPort.DisconnectAll();

                foreach (var connection in graph.edges[node.field])
                {
                    // connect to another field 
                    if (shownFieldNodes.TryGetValue(connection, out var outField))
                    {
                        var edge = node.inputPort.ConnectTo(outField.outputPort);
                        AddElement(edge);
                        edge.BringToFront();
                        outField.RefreshPorts();
                    }
                    // connect the input of this field to the output of an output field
                    else if (connection is VisualNode.OutputField 
                        && shownOutputFields.TryGetValue(connection as VisualNode.OutputField, out var outNode))
                    {
                        var edge = node.inputPort.ConnectTo(outNode.Item1);
                        AddElement(edge);
                        edge.BringToFront();
                        outNode.Item2.RefreshPorts();
                    }
                }
                node.RefreshPorts();
            }

            Sort();

            lastUpdateTime = Time.time;
        }

        void UpdateValues()
        {
            foreach (var node in shownVisualNodes.Values)
            {
                node.Update();
            }
            foreach (var node in shownFieldNodes.Values)
            {
                node.Update();
            }
        }

        HashSet<BaseField> GetRelatedFields(VisualNode visualNode, int depth = 5)
        {
            var outputFields = new HashSet<VisualNode.OutputField>(visualNode.GetOutputFields().Values);
            var res = new HashSet<BaseField>();
            Stack<(int, BaseField)> dfs = new Stack<(int, BaseField)>();

            foreach (var node in graph.nodes)
            {
                if (!res.Contains(node) && node is VisualNode.OutputField 
                    && outputFields.Contains(node as VisualNode.OutputField))
                {
                    dfs.Push((0, node));
                }
                while (dfs.Count > 0)
                {
                    var (n, current) = dfs.Pop();
                    if (n > depth) 
                        continue;

                    res.Add(current);

                    if (!graph.edges.TryGetValue(current, out var refs))
                        continue;

                    foreach (var son in refs)
                    {
                        if (!res.Contains(son))
                        {
                            dfs.Push((n + 1, son));
                        }
                    }
                }
            }
            return res;
        }

        void RefreshVisualNodes(IEnumerable<VisualNode> visualNodes)
        {
            HashSet<GraphViewVisualNode> relevantNodes = new HashSet<GraphViewVisualNode>();
            // create new nodes not existing in graph
            foreach (var node in visualNodes)
            {
                if (!shownVisualNodes.ContainsKey(node))
                {
                    CreateVisualNode(node);
                }
                relevantNodes.Add(shownVisualNodes[node]);
            }
            // remove nodes that were relevant last iteration but aren't anymore
            foreach (var node in nodes.ToList())
            {
                if (node is GraphViewVisualNode)
                {
                    var fnode = node as GraphViewVisualNode;
                    if (!relevantNodes.Contains(fnode))
                    {
                        RemoveVisualNode(fnode);
                    }
                }
            }
        }

        void RefreshFieldNodes(Rect position, IEnumerable<BaseField> graphNodes)
        {
            HashSet<GraphViewFieldNode> relevantNodes = new HashSet<GraphViewFieldNode>();
            // create new nodes not existing in graph
            foreach (var node in graphNodes)
            {
                if (node is VisualNode.OutputField)
                    continue;

                if (!shownFieldNodes.ContainsKey(node))
                {
                    CreateFieldNode(position, node);
                }
                relevantNodes.Add(shownFieldNodes[node]);
            }
            // remove nodes that were relevant last iteration but aren't anymore
            foreach (var node in nodes.ToList())
            {
                if (node is GraphViewFieldNode)
                {
                    var fnode = node as GraphViewFieldNode;
                    if (!relevantNodes.Contains(fnode))
                    {
                        RemoveFieldNode(fnode);
                    }
                }
            }
        }

        internal GraphViewVisualNode CreateVisualNode(VisualNode visualNode)
        {
            var node = new GraphViewVisualNode(this, visualNode);

            node.SetPosition(new Rect(new Vector2(100, 100),
                new Vector2(100, 100)));

            AddElement(node);
            shownVisualNodes.Add(visualNode, node);

            node.RefreshPorts();
            node.RefreshExpandedState();
            return node;
        }

        internal GraphViewFieldNode CreateFieldNode(Rect position, BaseField field)
        {
            var node = new GraphViewFieldNode(this, field);

            node.SetPosition(new Rect(new Vector2(position.x + 100, position.y + 100),
                new Vector2(100, 100)));

            AddElement(node);
            shownFieldNodes.Add(field, node);

            node.RefreshPorts();
            node.RefreshExpandedState();
            return node;
        }

        internal void RemoveVisualNode(GraphViewVisualNode fnode)
        {
            shownVisualNodes.Remove(fnode.visualNode);
            RemoveElement(fnode);
        }

        internal void RemoveFieldNode(GraphViewFieldNode fnode)
        {
            shownFieldNodes.Remove(fnode.field);
            RemoveElement(fnode);
        }

        private static Port GetPortInstance(UnityGraphNode node, Direction nodeDirection,
    Port.Capacity capacity = Port.Capacity.Multi)
        {
            return node.InstantiatePort(Orientation.Horizontal, nodeDirection, capacity, typeof(int));
        }


        internal class GraphViewFieldNode : UnityGraphNode
        {
            internal DebugGraphView view;
            internal BaseField field;
            internal Port inputPort;
            internal Port outputPort;
            internal Label valueLabel;

            public GraphViewFieldNode(DebugGraphView view, BaseField field) : base()
            {
                this.view = view;
                this.field = field;
                title = GetTitle();
                expanded = false;

                AddToClassList(GetFieldType());

                valueLabel = new Label();
                valueLabel.name = "value";
                titleContainer.Add(valueLabel);

                {
                    inputPort = GetPortInstance(this, Direction.Input);
                    var portLabel = inputPort.contentContainer.Q<Label>("type");
                    inputPort.contentContainer.Remove(portLabel);
                    inputPort.contentContainer.Add(new Label(" "));
                    inputPort.name = name;
                    inputPort.portName = name;
                    inputContainer.Add(inputPort);
                }
                {
                    outputPort = GetPortInstance(this, Direction.Output);
                    var portLabel = outputPort.contentContainer.Q<Label>("type");
                    outputPort.contentContainer.Remove(portLabel);
                    outputPort.contentContainer.Add(new Label(" "));
                    outputPort.name = name;
                    outputPort.portName = name;
                    outputContainer.Add(outputPort);
                }
            }

            string GetTitle()
            {
                if (field is VisualNode.OutputField)
                {
                    var fnode = field as VisualNode.OutputField;
                    return $"Output: {fnode.name}";
                }
                return GetFieldType();
            }
            string GetFieldType() => field.GetType().Name;

            public void Update()
            {
                valueLabel.ClearClassList();

                var val = field.GetValue();
                valueLabel.text = val.ToString();

                valueLabel.EnableInClassList("non-zero", val != 0);
                valueLabel.EnableInClassList("zero", val == 0);
            }
        }

        internal class GraphViewVisualNode : UnityGraphNode
        {
            internal DebugGraphView view;
            internal VisualNode visualNode;
            internal Dictionary<VisualNode.OutputField, (Port, Port, Label)> outputs
                = new Dictionary<VisualNode.OutputField, (Port, Port, Label)>();

            public GraphViewVisualNode(DebugGraphView view, VisualNode visualNode) : base()
            {
                this.view = view;
                this.visualNode = visualNode;
                title = visualNode.name;
                expanded = false;

                foreach (var kv in visualNode.GetOutputFields().OrderBy(f => f.Key))
                {
                    var name = kv.Key;
                    var field = kv.Value;

                    Port ingoing, outgoing;
                    Label valLabel;
                    {
                        var port = ingoing = GetPortInstance(this, Direction.Input);

                        var portLabel = port.contentContainer.Q<Label>("type");
                        port.contentContainer.Remove(portLabel);

                        valLabel = new Label();
                        valLabel.name = "value";

                        port.contentContainer.Add(new Label(name));
                        port.contentContainer.Add(valLabel);
                        port.name = name;
                        port.portName = name;
                        inputContainer.Add(port);
                    }
                    {
                        var port = outgoing = GetPortInstance(this, Direction.Output);

                        var portLabel = port.contentContainer.Q<Label>("type");
                        port.contentContainer.Remove(portLabel);
                        outputContainer.Add(port);
                    }
                    outputs[field] = (ingoing, outgoing, valLabel);
                }
            }

            public override void OnSelected()
            {
                // TODO assign button / double-click:
                //UnityEditor.Selection.activeGameObject = visualNode.gameObject;

                // force refresh
                view.lastUpdateTime = 0;
            }

            public void Update()
            {
                RefreshValue();
            }

            void RefreshValue()
            {
                foreach (var kv in outputs)
                {
                    var (ingoing, outgoing, label) = kv.Value;
                    label.ClearClassList();

                    var val = kv.Key.GetValue();

                    var fd = Manager.Instance.GetFieldDefinition(kv.Key.name);
                    if (fd.Value.Type == VisualNode.FieldType.Boolean)
                    {
                        label.text = val == 1 ? "true" : "false";
                        label.EnableInClassList(label.text, true);
                    }
                    else
                    {
                        label.text = fd.Value.EnumValues[val];
                    }
                }
            }
        }
    }
}