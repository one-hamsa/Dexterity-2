using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins.Utilities
{
    public class StaggerChildrenState : MonoBehaviour, IReferencesNodes
    {
        public float firstDelay = 0.2f;
        public float perChildDelay = 0.2f;
        
        [State(allowEmpty: true)]
        public string fromState;
        
        [State(allowEmpty: true)]
        public string toState;

        private List<BaseStateNode> nodes = new();
        private Dictionary<BaseStateNode, Action<int, int>> onStateChanged = new();

        private int fromStateId;
        private int toStateId;
        
        public List<BaseStateNode> GetNodes()
        {
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
            fromStateId = Database.instance.GetStateID(fromState);
            toStateId = Database.instance.GetStateID(toState);
        }

        private void OnEnable()
        {
            isStaggerDone = false;
            
            var delay = firstDelay;
            foreach (var node in GetNodes())
            {
                node.SetStateDelay(fromStateId, toStateId, delay);
                
                void OnNodeStateChanged(int oldState, int newState)
                {
                    if (toStateId == StateFunction.emptyStateId || newState == toStateId)
                    {
                        node.SetStateDelay(fromStateId, toStateId, 0);
                        node.onStateChanged -= onStateChanged[node];
                        onStateChanged.Remove(node);
                        
                        if (onStateChanged.Count == 0)
                            isStaggerDone = true;
                    }
                }
                node.onStateChanged += OnNodeStateChanged;
                onStateChanged[node] = OnNodeStateChanged;
                
                if (fromStateId != StateFunction.emptyStateId)
                    node.JumpToState(fromStateId);
                
                delay += perChildDelay;
            }
        }
        
        private void OnDisable()
        {
            foreach (var node in GetNodes())
            {
                if (onStateChanged.TryGetValue(node, out var onNodeStateChanged))
                {
                    node.SetStateDelay(fromStateId, toStateId, 0);
                    node.onStateChanged -= onNodeStateChanged;
                    onStateChanged.Remove(node);
                }
            }
        }
    }
}