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

        public void SaveData(StateFunction sfContainerObject)
        {
            if (!SaveNodes(sfContainerObject))
            {
                Debug.LogError($"SaveNodes() failed");
                return;
            }

            EditorUtility.SetDirty(sfContainerObject);
            AssetDatabase.SaveAssets();
        }

        private bool SaveNodes(StateFunction sfContainerObject)
        {
            sfContainerObject.nodeLinks.Clear();
            sfContainerObject.conditionNodeData.Clear();
            sfContainerObject.decisionNodeData.Clear();

            var connectedSockets = Edges.Where(x => x.input.node != null).ToArray();
            for (var i = 0; i < connectedSockets.Count(); i++)
            {
                var outputNode = (BaseStateNode)connectedSockets[i].output.node;
                var inputNode = (BaseStateNode)connectedSockets[i].input.node;

                if (inputNode == null || outputNode == null)
                    continue;

                sfContainerObject.nodeLinks.Add(new NodeLinkData
                {
                    baseNodeGUID = outputNode.GUID,
                    targetNodeGUID = inputNode.GUID,
                    basePort = connectedSockets[i].output.portName,
                });
            }

            foreach (var node in Nodes)
            {
                switch (node)
                {
                    case ConditionNode cNode:
                        sfContainerObject.conditionNodeData.Add(new ConditionNodeData
                        {
                            nodeGUID = cNode.GUID,
                            field = cNode.Field,
                            freeText = cNode.FreeText,
                            entryPoint = cNode.EntryPoint,
                            position = cNode.GetPosition().position
                        });
                        break;
                    case DecisionNode dNode:
                        sfContainerObject.decisionNodeData.Add(new DecisionNodeData
                        {
                            nodeGUID = dNode.GUID,
                            state = dNode.State,
                            freeText = dNode.FreeText,
                            position = dNode.GetPosition().position
                        });
                        break;
                }
            }

            return true;
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
            foreach (var perNode in _sfContainer.decisionNodeData)
            {
                var tempNode = _graphView.CreateDecisionNode(perNode.position, perNode.freeText, perNode.state);
                tempNode.GUID = perNode.nodeGUID;
                _graphView.AddElement(tempNode);
            }

            foreach (var perNode in _sfContainer.conditionNodeData)
            {
                var tempNode = _graphView.CreateConditionNode(perNode.position, perNode.freeText, 
                    perNode.field, perNode.entryPoint);
                tempNode.GUID = perNode.nodeGUID;
                _graphView.AddElement(tempNode);
            }
        }

        private void ConnectNodes()
        {
            foreach (var edge in _sfContainer.nodeLinks)
            {
                var baseNode = Nodes.Where(n => edge.baseNodeGUID == n.GUID).First();
                var targetNode = Nodes.Where(n => edge.targetNodeGUID == n.GUID).First();

                var basePort = baseNode?.outputContainer.Q<Port>(edge.basePort);
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
    }
}