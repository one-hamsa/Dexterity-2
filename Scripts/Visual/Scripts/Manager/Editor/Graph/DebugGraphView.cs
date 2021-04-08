using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

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
            //styleSheets.Add(Resources.Load<StyleSheet>("Graph"));
            SetupZoom(ContentZoomer.DefaultMinScale * 2, ContentZoomer.DefaultMaxScale * 2);

            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new FreehandSelector());
        }


        Dictionary<BaseField, FieldNode> shownNodes = new Dictionary<BaseField, FieldNode>();
        public void Update()
        {
            if (!Application.isPlaying || !shouldUpdate || Time.time - updateTimeDelta < lastUpdateTime
                || graph.lastSuccessfulUpdate <= lastUpdateTime)
                return;

            HashSet<FieldNode> relevantNodes = new HashSet<FieldNode>();
            foreach (var node in graph.nodes)
            {
                if (!shownNodes.ContainsKey(node))
                {
                    shownNodes[node] = CreateNode(node);
                }
                relevantNodes.Add(shownNodes[node]);
            }
            foreach (var node in nodes.ToList())
            {
                var fnode = node as FieldNode;
                if (!relevantNodes.Contains(fnode))
                {
                    shownNodes.Remove(fnode.field);
                    RemoveElement(fnode);
                }
            }


            lastUpdateTime = Time.time;
        }

        internal FieldNode CreateNode(BaseField field)
        {
            var tempNode = new FieldNode(field);

            tempNode.RefreshPorts();
            tempNode.SetPosition(new Rect(new Vector2(100, 100),
                new Vector2(100, 100)));
            tempNode.RefreshExpandedState();

            AddElement(tempNode);
            return tempNode;
        }


        internal class FieldNode : UnityEditor.Experimental.GraphView.Node
        {
            internal BaseField field;
            public FieldNode(BaseField field) : base()
            {
                this.field = field;
                title = field.ToString();
            }
        }
    }
}