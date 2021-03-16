using System;
using System.Collections;
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
        public Blackboard Blackboard = new Blackboard();
        public List<ExposedProperty> ExposedProperties { get; private set; } = new List<ExposedProperty>();
        private NodeSearchWindow _searchWindow;

        StateFunctionGraph _parent;
        void RegisterOnRefresh(UnityEditor.Experimental.GraphView.Node node, Action action)
        {
            _parent.OnRefresh += action;
        }

        public StateFunctionGraphView(StateFunctionGraph editorWindow)
        {
            styleSheets.Add(Resources.Load<StyleSheet>("Graph"));
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


        public void ClearBlackBoardAndExposedProperties()
        {
            ExposedProperties.Clear();
            Blackboard.Clear();
        }

        public void AddPropertyToBlackBoard(ExposedProperty property, bool loadMode = false)
        {
            var localPropertyName = property.PropertyName;
            var localPropertyValue = property.PropertyValue;
            if (!loadMode)
            {
                while (ExposedProperties.Any(x => x.PropertyName == localPropertyName))
                    localPropertyName = $"{localPropertyName}(1)";
            }

            var item = ExposedProperty.CreateInstance();
            item.PropertyName = localPropertyName;
            item.PropertyValue = localPropertyValue;
            ExposedProperties.Add(item);

            var container = new VisualElement();
            var field = new BlackboardField {text = localPropertyName, typeText = "string"};
            container.Add(field);

            var propertyValueTextField = new TextField("Value:")
            {
                value = localPropertyValue
            };
            propertyValueTextField.RegisterValueChangedCallback(evt =>
            {
                var index = ExposedProperties.FindIndex(x => x.PropertyName == item.PropertyName);
                ExposedProperties[index].PropertyValue = evt.newValue;
            });
            var sa = new BlackboardRow(field, propertyValueTextField);
            container.Add(sa);
            Blackboard.Add(container);
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
                Debug.Log(evt.newValue);
                if (evt.newValue)
                    tempNode.AddToClassList("entry");
                else
                    tempNode.RemoveFromClassList("entry");
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
            });
            textField.SetValueWithoutNotify(tempNode.title);
            tempNode.mainContainer.Add(textField);

            tempNode.RefreshPorts();
            tempNode.RefreshExpandedState();

            DrawConditionNodeDynamic(tempNode);
            RegisterOnRefresh(tempNode, () => DrawConditionNodeDynamic(tempNode));

            return tempNode;
        }

        string GetConditionNodeTitle(ConditionNode node)
        {
            string extraText = "";
            if (node.Field.Length > 0)
            {
                switch (Manager.Instance.GetFieldDefinition(node.Field).Value.Type)
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
                toolbar.menu.AppendAction(field.Name, evt => SetConditionField(evt, node));
            }
            node.outputContainer.Add(toolbar);

            var newPorts = new List<Port>();
            if (node.Field.Length > 0)
            {
                var field = Manager.Instance.GetFieldDefinition(node.Field).Value;
                switch (field.Type)
                {
                    case Node.FieldType.Boolean:
                        newPorts.Add(AddFieldPort(node, "true"));
                        newPorts.Add(AddFieldPort(node, "false"));
                        break;
                    case Node.FieldType.Enum:
                        foreach (var value in field.EnumValues)
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