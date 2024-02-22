using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.Serialization;

namespace OneHamsa.Dexterity.Builtins.Utilities
{
    public class StaggerChildrenState : MonoBehaviour, IReferencesNodes
    {
        public float firstDelay = 0.2f;
        public float perChildDelay = 0.2f;
        
        [State(allowEmpty: true)]
        public string forceSourceState;
        
        [State(allowEmpty: true)]
        public string destinationState;
        
        public List<BaseStateNode> manualNodes = new();

        public bool skipIfNotInDestinationState;

        private List<BaseStateNode> nodes = new();
        private Dictionary<BaseStateNode, Action<int, int>> onStateChanged = new();

        private int forceSourceStateId;
        private int destStateId;
        
        public List<BaseStateNode> GetNodes()
        {
            if (manualNodes.Count > 0)
                return manualNodes;
            
            nodes.Clear();
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                var node = child.GetComponent<BaseStateNode>();
                if (node)
                    nodes.Add(node);
            }
            return nodes;
        }
        
        [Preserve]
        public bool isStaggerDone { get; private set; }

        private void Awake()
        {
            forceSourceStateId = Database.instance.GetStateID(forceSourceState);
            destStateId = Database.instance.GetStateID(destinationState);
        }

        private void Start()
        {
            isStaggerDone = false;
            
            var delay = firstDelay;
            foreach (var node in GetNodes())
            {
                if (skipIfNotInDestinationState && node.GetNextState() != destStateId)
                    continue;
                
                node.SetStateDelay(StateFunction.emptyStateId, destStateId, delay);
                
                void OnNodeStateChanged(int oldState, int newState)
                {
                    if (destStateId == StateFunction.emptyStateId || newState == destStateId)
                    {
                        node.SetStateDelay(StateFunction.emptyStateId, destStateId, 0);
                        node.onStateChanged -= onStateChanged[node];
                        onStateChanged.Remove(node);
                        
                        if (onStateChanged.Count == 0)
                            isStaggerDone = true;
                    }
                }
                node.onStateChanged += OnNodeStateChanged;
                onStateChanged[node] = OnNodeStateChanged;
                
                if (forceSourceStateId != StateFunction.emptyStateId)
                    node.JumpToState(forceSourceStateId);
               
                delay += perChildDelay;
            }
        }
        
        private void OnDisable()
        {
            foreach (var node in GetNodes())
            {
                if (onStateChanged.TryGetValue(node, out var onNodeStateChanged))
                {
                    node.SetStateDelay(forceSourceStateId, destStateId, 0);
                    node.onStateChanged -= onNodeStateChanged;
                    onStateChanged.Remove(node);
                }
            }
        }
    }
}