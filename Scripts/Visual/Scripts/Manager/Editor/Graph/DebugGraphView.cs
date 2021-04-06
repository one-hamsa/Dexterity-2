using System;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace OneHamsa.Dexterity.Visual
{
    public class DebugGraphView : GraphView
    {
        public bool shouldUpdate = true;
        public float lastUpdateTime = -1;
        Graph graph => Manager.Instance.graph;

        public DebugGraphView(DebugWindow editorWindow)
        {
            //styleSheets.Add(Resources.Load<StyleSheet>("Graph"));
            SetupZoom(ContentZoomer.DefaultMinScale * 2, ContentZoomer.DefaultMaxScale * 2);

            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new FreehandSelector());
        }

        public void Update()
        {
            if (!Application.isPlaying || !shouldUpdate || lastUpdateTime >= graph.lastSuccessfulUpdate)
                return;

            DeleteElements(edges.ToList());
            DeleteElements(nodes.ToList());

            foreach (var node in graph.nodes)
            {
                CreateNode(node);
            }
        }

        public void CreateNode(BaseField field)
        {
            var tempNode = new FieldNode(field);

            tempNode.RefreshPorts();
            tempNode.SetPosition(new Rect(new Vector2(100, 100),
                new Vector2(100, 100)));
            tempNode.RefreshExpandedState();

            AddElement(tempNode);
        }


        internal class FieldNode : UnityEditor.Experimental.GraphView.Node
        {
            private BaseField field;
            public FieldNode(BaseField field) : base()
            {
                this.field = field;
                title = field.ToString();
            }
        }
    }
}