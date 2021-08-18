using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

// based on https://github.com/merpheus-dev/NodeBasedDialogueSystem
namespace OneHamsa.Dexterity.Visual
{
    public class StateFunctionGraphView : GraphView
    {
        public readonly Vector2 DefaultNodeSize = new Vector2(200, 150);
        private NodeSearchWindow _searchWindow;
        private StateFunctionGraph _parent;

        public StateFunctionGraphView(StateFunctionGraph editorWindow)
        {
            styleSheets.Add(Resources.Load<StyleSheet>("StateFunctionGraph"));
            SetupZoom(ContentZoomer.DefaultMinScale * 2, ContentZoomer.DefaultMaxScale * 2);

            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new FreehandSelector());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            AddSearchWindow(editorWindow);
            _parent = editorWindow;

            RegisterCallback<MouseUpEvent>(OnMouseUp);
            graphViewChanged = OnGraphChanged;
        }

        void SetDirty()
        {
            EditorUtility.SetDirty(_parent.data);
        }

        private GraphViewChange OnGraphChanged(GraphViewChange graphViewChange)
        {
            SetDirty();
            return graphViewChange;
        }

        void OnMouseUp(MouseUpEvent evt)
        {
            Vector2 pos = evt.originalMousePosition;
            pos = contentViewContainer.WorldToLocal(pos);

            if (Keyboard.current[Key.C].isPressed)
                CreateNewConditionNode(pos);
            else if (Keyboard.current[Key.D].isPressed)
                CreateNewDecisionNode(pos);
        }


        private void AddSearchWindow(StateFunctionGraph editorWindow)
        {
            _searchWindow = ScriptableObject.CreateInstance<NodeSearchWindow>();
            _searchWindow.Configure(editorWindow, this);
            nodeCreationRequest = context =>
                SearchWindow.Open(new SearchWindowContext(context.screenMousePosition), _searchWindow);
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var compatiblePorts = new List<Port>();
            var startPortView = startPort;

            ports.ForEach((port) =>
            {
                var portView = port;
                if (startPortView != portView && startPortView.node != portView.node)
                    compatiblePorts.Add(port);
            });

            return compatiblePorts;
        }

        public void CreateNewConditionNode(Vector2 position, string nodeName = "", string fieldName = "", bool entry = false)
        {
            entry |= nodes.ToList().Where(n => n is ConditionNode).Count() == 0;
            AddElement(CreateConditionNode(position, nodeName, fieldName, entry));
        }
        public void CreateNewDecisionNode(Vector2 position, string nodeName = "", string stateName = "")
        {
            AddElement(CreateDecisionNode(position, nodeName, stateName));
        }

        public ConditionNode CreateConditionNode(Vector2 position, string nodeName, string fieldName, bool entry)
        {
            var tempNode = new ConditionNode()
            {
                title = nodeName,
                FreeText = nodeName,
                Field = fieldName,
                EntryPoint = entry,
                GUID = Guid.NewGuid().ToString()
            };

            tempNode.styleSheets.Add(Resources.Load<StyleSheet>("Node"));

            var checkbox = new UnityEngine.UIElements.Toggle("");
            checkbox.RegisterValueChangedCallback(evt =>
            {
                tempNode.EntryPoint = evt.newValue;
                if (evt.newValue)
                    tempNode.AddToClassList("entry");
                else
                    tempNode.RemoveFromClassList("entry");

                SetDirty();
            });
            checkbox.value = entry;
            tempNode.titleButtonContainer.Add(checkbox);

            var inputPort = GetPortInstance(tempNode, Direction.Input, Port.Capacity.Multi);
            inputPort.portName = "";
            tempNode.inputContainer.Add(inputPort);

            tempNode.RefreshExpandedState();
            tempNode.RefreshPorts();
            tempNode.SetPosition(new Rect(position,
                DefaultNodeSize)); //To-Do: implement screen center instantiation positioning

            var textField = new TextField("");
            textField.RegisterValueChangedCallback(evt =>
            {
                tempNode.FreeText = evt.newValue;
                tempNode.title = GetConditionNodeTitle(tempNode);
                SetDirty();

            });
            textField.SetValueWithoutNotify(tempNode.title);
            tempNode.mainContainer.Add(textField);

            tempNode.RefreshPorts();
            tempNode.RefreshExpandedState();

            DrawConditionNodeDynamic(tempNode);

            return tempNode;
        }

        string GetConditionNodeTitle(ConditionNode node)
        {
            string extraText = "";
            if (node.Field.Length > 0)
            {
                switch (Manager.Instance.GetFieldDefinition(node.Field).Value.type)
                {
                    case Node.FieldType.Boolean:
                        extraText = "?";
                        break;
                    case Node.FieldType.Enum:
                        extraText = " = ?";
                        break;
                }
            }
            var fieldText = node.Field.Length > 0 ? $"{node.Field}{extraText}" : "<unassigned>";
            if (node.FreeText.Length > 0)
                return $"{node.FreeText} ({fieldText})";

            return fieldText;
        }

        void DrawConditionNodeDynamic(ConditionNode node)
        {
            var connectedPorts = node.outputContainer.Query<Port>()
                .Where(p => p.connected).ToList();

            // clear
            node.outputContainer.Clear();

            // rebuild
            node.outputContainer.Add(new Label("Field: "));
            var toolbar = new ToolbarMenu();
            toolbar.text = node.Field?.Length > 0 ? node.Field : "(Select)";
            foreach (var field in Manager.Instance.FieldDefinitions)
            {
                toolbar.menu.AppendAction(field.name, evt => SetConditionField(evt, node));
            }
            node.outputContainer.Add(toolbar);

            var newPorts = new List<Port>();
            if (node.Field.Length > 0)
            {
                var field = Manager.Instance.GetFieldDefinition(node.Field).Value;
                switch (field.type)
                {
                    case Node.FieldType.Boolean:
                        newPorts.Add(AddFieldPort(node, "true"));
                        newPorts.Add(AddFieldPort(node, "false"));
                        break;
                    case Node.FieldType.Enum:
                        foreach (var value in field.enumValues)
                        {
                            newPorts.Add(AddFieldPort(node, value));
                        }
                        break;
                }
            }

            foreach (var oldPort in connectedPorts)
            {
                foreach (var edge in oldPort.connections)
                {
                    RemoveElement(edge);
                }
            }

            node.title = GetConditionNodeTitle(node);

            node.ClearClassList();
            var connectedPortCount = connectedPorts.Count;
            // TODO if (connectedPortCount < )... add error

            node.RefreshPorts();
            node.RefreshExpandedState();
        }

        void SetConditionField(DropdownMenuAction evt, ConditionNode node)
        {
            node.Field = evt.name;
            DrawConditionNodeDynamic(node);
        }

        public DecisionNode CreateDecisionNode(Vector2 position, string nodeName, string stateName)
        {
            var tempNode = new DecisionNode()
            {
                title = nodeName,
                FreeText = nodeName,
                State = stateName,
                GUID = Guid.NewGuid().ToString()
            };
            tempNode.styleSheets.Add(Resources.Load<StyleSheet>("Node"));
            var inputPort = GetPortInstance(tempNode, Direction.Input, Port.Capacity.Multi);
            inputPort.portName = "";
            tempNode.inputContainer.Add(inputPort);
            tempNode.RefreshExpandedState();
            tempNode.RefreshPorts();
            tempNode.SetPosition(new Rect(position,
                DefaultNodeSize)); //To-Do: implement screen center instantiation positioning

            var freeTextField = new TextField("");
            freeTextField.RegisterValueChangedCallback(evt =>
            {
                tempNode.FreeText = evt.newValue;
                tempNode.title = GetDecisionNodeTitle(tempNode);
                SetDirty();
            });
            freeTextField.SetValueWithoutNotify(tempNode.title);
            tempNode.mainContainer.Add(freeTextField);

            tempNode.outputContainer.Add(new Label("State:"));
            var stateField = new TextField();
            stateField.AddToClassList("state");
            stateField.RegisterValueChangedCallback(evt =>
            {
                tempNode.State = evt.newValue;
                tempNode.title = GetDecisionNodeTitle(tempNode);
                SetDirty();
            });
            stateField.SetValueWithoutNotify(tempNode.State);
            tempNode.outputContainer.Add(stateField);

            tempNode.title = GetDecisionNodeTitle(tempNode);

            tempNode.RefreshPorts();
            tempNode.RefreshExpandedState();

            return tempNode;
        }

        string GetDecisionNodeTitle(DecisionNode node)
        {
            var stateText = node.State.Length > 0 ? $"{node.State}" : "<unassigned>";
            if (node.FreeText.Length > 0)
                return $"{node.FreeText} ({stateText})";

            return stateText;
        }


        public Port AddFieldPort(UnityEditor.Experimental.GraphView.Node nodeCache, 
            string outputPortName)
        {
            var generatedPort = GetPortInstance(nodeCache, Direction.Output);
            var portLabel = generatedPort.contentContainer.Q<Label>("type");
            generatedPort.contentContainer.Remove(portLabel);


            generatedPort.contentContainer.Add(new Label($"  {outputPortName}"));
            generatedPort.name = outputPortName;
            generatedPort.portName = outputPortName;
            nodeCache.outputContainer.Add(generatedPort);
            nodeCache.RefreshPorts();
            nodeCache.RefreshExpandedState();

            return generatedPort;
        }

        private Port GetPortInstance(UnityEditor.Experimental.GraphView.Node node, Direction nodeDirection,
            Port.Capacity capacity = Port.Capacity.Single)
        {
            return node.InstantiatePort(Orientation.Horizontal, nodeDirection, capacity, typeof(int));
        }
    }
}