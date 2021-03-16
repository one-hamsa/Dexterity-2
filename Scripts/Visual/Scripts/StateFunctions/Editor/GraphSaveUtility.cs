using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace OneHamsa.Dexterity.Visual
{
    public class GraphSaveUtility
    {
        private List<Edge> Edges => _graphView.edges.ToList();
        private List<BaseStateNode> Nodes => _graphView.nodes.ToList().Cast<BaseStateNode>().ToList();

        private StateFunction _sfContainer;
        private StateFunctionGraphView _graphView;

        public static GraphSaveUtility GetInstance(StateFunctionGraphView graphView)
        {
            return new GraphSaveUtility
            {
                _graphView = graphView
            };
        }

        public void SaveGraph(string fileName)
        {
            var sfContainerObject = ScriptableObject.CreateInstance<StateFunction>();
            if (!SaveNodes(sfContainerObject))
            {
                Debug.LogError($"SaveNodes() failed");
                return;
            }
            SaveExposedProperties(sfContainerObject);

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            UnityEngine.Object loadedAsset = AssetDatabase.LoadAssetAtPath($"Assets/Resources/{fileName}.asset", typeof(StateFunction));

            if (loadedAsset == null || !AssetDatabase.Contains(loadedAsset))
            {
                AssetDatabase.CreateAsset(sfContainerObject, $"Assets/Resources/{fileName}.asset");
            }
            else
            {
                var container = loadedAsset as StateFunction;
                container.NodeLinks = sfContainerObject.NodeLinks;
                container.ConditionNodeData = sfContainerObject.ConditionNodeData;
                container.DecisionNodeData = sfContainerObject.DecisionNodeData;
                container.ExposedProperties = sfContainerObject.ExposedProperties;
                EditorUtility.SetDirty(container);
            }

            AssetDatabase.SaveAssets();
        }

        private bool SaveNodes(StateFunction sfContainerObject)
        {
            var connectedSockets = Edges.Where(x => x.input.node != null).ToArray();
            for (var i = 0; i < connectedSockets.Count(); i++)
            {
                var outputNode = (BaseStateNode)connectedSockets[i].output.node;
                var inputNode = (BaseStateNode)connectedSockets[i].input.node;

                if (inputNode == null || outputNode == null)
                    continue;

                sfContainerObject.NodeLinks.Add(new NodeLinkData
                {
                    BaseNodeGUID = outputNode.GUID,
                    TargetNodeGUID = inputNode.GUID,
                    BasePort = connectedSockets[i].output.portName,
                });
            }

            foreach (var node in Nodes)
            {
                switch (node)
                {
                    case ConditionNode cNode:
                        sfContainerObject.ConditionNodeData.Add(new ConditionNodeData
                        {
                            NodeGUID = cNode.GUID,
                            Field = cNode.Field,
                            FreeText = cNode.FreeText,
                            EntryPoint = cNode.EntryPoint,
                            Position = cNode.GetPosition().position
                        });
                        break;
                    case DecisionNode dNode:
                        sfContainerObject.DecisionNodeData.Add(new DecisionNodeData
                        {
                            NodeGUID = dNode.GUID,
                            State = dNode.State,
                            FreeText = dNode.FreeText,
                            Position = dNode.GetPosition().position
                        });
                        break;
                }
            }

            return true;
        }

        private void SaveExposedProperties(StateFunction sfContainer)
        {
            sfContainer.ExposedProperties.Clear();
            sfContainer.ExposedProperties.AddRange(_graphView.ExposedProperties);
        }

        public void LoadData(string fileName) => LoadData(Resources.Load<StateFunction>(fileName));
        public void LoadData(StateFunction container)
        {
            _sfContainer = container;
            if (_sfContainer == null)
            {
                EditorUtility.DisplayDialog("File Not Found", "Target Data does not exist!", "OK");
                return;
            }

            ClearGraph();
            GenerateNodes();
            ConnectNodes();
            AddExposedProperties();
        }

        /// <summary>
        /// Set Entry point GUID then Get All Nodes, remove all and their edges. Leave only the entrypoint node. (Remove its edge too)
        /// </summary>
        private void ClearGraph()
        {
            foreach (var perNode in Nodes)
            {
                _graphView.RemoveElement(perNode);
            }
            foreach (var perEdge in Edges)
            {
                _graphView.RemoveElement(perEdge);
            }
        }

        /// <summary>
        /// Create All serialized nodes and assign their guid and dialogue text to them
        /// </summary>
        private void GenerateNodes()
        {
            foreach (var perNode in _sfContainer.DecisionNodeData)
            {
                var tempNode = _graphView.CreateDecisionNode(perNode.Position, perNode.FreeText, perNode.State);
                tempNode.GUID = perNode.NodeGUID;
                _graphView.AddElement(tempNode);
            }

            foreach (var perNode in _sfContainer.ConditionNodeData)
            {
                var tempNode = _graphView.CreateConditionNode(perNode.Position, perNode.FreeText, 
                    perNode.Field, perNode.EntryPoint);
                tempNode.GUID = perNode.NodeGUID;
                _graphView.AddElement(tempNode);
            }
        }

        private void ConnectNodes()
        {
            foreach (var edge in _sfContainer.NodeLinks)
            {
                var baseNode = Nodes.Where(n => edge.BaseNodeGUID == n.GUID).First();
                var targetNode = Nodes.Where(n => edge.TargetNodeGUID == n.GUID).First();

                var basePort = baseNode?.outputContainer.Q<Port>(edge.BasePort);
                var targetPort = targetNode?.inputContainer.Q<Port>();

                if (basePort != null && targetPort != null)
                {
                    _graphView.Add(basePort.ConnectTo(targetPort));
                }
                else
                {
                    Debug.Log($"bn = {baseNode.title} bp= {basePort}, tn = {targetNode.title} tp = {targetPort}");
                }
            }
        }

        private void AddExposedProperties()
        {
            _graphView.ClearBlackBoardAndExposedProperties();
            foreach (var exposedProperty in _sfContainer.ExposedProperties)
            {
                _graphView.AddPropertyToBlackBoard(exposedProperty);
            }
        }
    }
}