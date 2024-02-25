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
        
        [State]
        public string sourceState;
        
        [State]
        public string destinationState;
        
        public List<BaseStateNode> manualNodes = new();

        public bool skipIfNotInDestinationState;

        private List<BaseStateNode> nodes = new();
        private BaseStateNode current;
        private Dictionary<BaseStateNode, Action<int, int>> onStateChanged = new();

        private int sourceStateId;
        private int destStateId;
        
        public List<BaseStateNode> GetNodes()
        {
            nodes.Clear();

            if (manualNodes.Count > 0)
            {
                nodes.AddRange(manualNodes);
                for (var i = nodes.Count - 1; i >= 0; i--)
                {
                    if (!nodes[i].gameObject.activeSelf)
                        nodes.RemoveAt(i);
                }
                
                return nodes;
            }
            
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (!child.gameObject.activeSelf)
                    continue;
                
                var node = child.GetComponent<BaseStateNode>();
                if (node)
                    nodes.Add(node);
            }
            return nodes;
        }
        
        [Preserve]
        public bool isStaggerDone { get; private set; }
        public event Action onStaggerDone;

        private void Awake()
        {
            sourceStateId = Database.instance.GetStateID(sourceState);
            destStateId = Database.instance.GetStateID(destinationState);
        }

        private void Start()
        {
            isStaggerDone = false;
            current = null;
            
            foreach (var node in GetNodes())
                node.SetStateOverride(sourceStateId);

            // only start now if not recursive
            if (transform.parent == null || transform.parent.GetComponentInParent<StaggerChildrenState>() == null)
                Next();
        }

        private void Next()
        {
            var allNodes = GetNodes();
            
            // find next
            var index = allNodes.IndexOf(current);
            if (index == -1)
                index = 0;
            else
                index++;

            if (index >= GetNodes().Count)
            {
                // done
                isStaggerDone = true;
                onStaggerDone?.Invoke();
                return;
            }
            
            // stagger next
            current = allNodes[index];

            if (skipIfNotInDestinationState && current.GetNextStateWithoutOverride() != destStateId)
            {
                Next();
                return;
            }

            current.SetStateDelay(StateFunction.emptyStateId, destStateId, 
                index == 0 ? firstDelay : perChildDelay);
            
            void OnNodeStateChanged(int oldState, int newState)
            {
                if (destStateId == StateFunction.emptyStateId || newState == destStateId)
                {
                    current.SetStateDelay(StateFunction.emptyStateId, destStateId, 0);
                    current.onStateChanged -= onStateChanged[current];

                    void OnComplete()
                    {
                        onStateChanged.Remove(current);
                        Next();
                    }
                    
                    // try to recurse
                    var innerStagger = current.GetComponentInChildren<StaggerChildrenState>();
                    if (innerStagger != null && !innerStagger.isStaggerDone)
                    {
                        innerStagger.Next();
                        void OnInnerStaggerDone()
                        {
                            innerStagger.onStaggerDone -= OnInnerStaggerDone;
                            OnComplete();
                        }
                        innerStagger.onStaggerDone += OnInnerStaggerDone;
                    }
                    else
                        OnComplete();
                }
            }
            current.onStateChanged += OnNodeStateChanged;
            onStateChanged[current] = OnNodeStateChanged;
            
            if (sourceStateId != StateFunction.emptyStateId)
                current.JumpToState(sourceStateId);
            
            current.ClearStateOverride();
        }
        
        private void OnDisable()
        {
            foreach (var node in GetNodes())
            {
                if (onStateChanged.TryGetValue(node, out var onNodeStateChanged))
                {
                    node.SetStateDelay(sourceStateId, destStateId, 0);
                    node.onStateChanged -= onNodeStateChanged;
                    onStateChanged.Remove(node);
                    // TODO remove onStaggerDone recursive subscription
                }
            }
        }
    }
}