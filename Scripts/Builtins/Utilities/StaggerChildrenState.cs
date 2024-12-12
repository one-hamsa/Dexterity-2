using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins.Utilities
{
    public class StaggerChildrenState : MonoBehaviour, IReferencesNodes
    {
        public const float dontOverrideDelay = -1f;
        
        [Serializable]
        public class ManualNode
        {
            public BaseStateNode node;
            [Tooltip("Override delay for this node, -1 to not override")]
            public float overrideDelay = dontOverrideDelay;
        }
        
        public float firstDelay = 0.2f;
        public float perChildDelay = 0.2f;
        
        [State]
        public string sourceState;
        
        [State]
        public string destinationState;
        
        public List<ManualNode> manualNodes = new();

        [Tooltip("If true, will skip nodes that are not in (or transitioning to) destination state when stagger starts")]
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
                foreach (var manualNode in manualNodes)
                {
                    if (manualNode.node == null)
                        continue;
                    
                    if (!manualNode.node.gameObject.activeSelf)
                        continue;
                    
                    nodes.Add(manualNode.node);
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

        private void OnEnable()
        {
            StartCoroutine(StartStagger());
        }
        
        private void OnDisable()
        {
            StopAllCoroutines();
            
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

        private IEnumerator StartStagger()
        {
            isStaggerDone = false;
            current = null;

            // wait for start
            yield return new WaitForEndOfFrame();

            foreach (var node in GetNodes())
            {
                node.SetStateOverride(sourceStateId);
                node.JumpToState();
            }

            // wait for all gameObjects to get Awake() and OnEnable()
            yield return null;

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

            if (current.initialized == false) 
            {
                Next();
                return;
            }
            
            if (skipIfNotInDestinationState && current.GetNextStateWithoutOverride() != destStateId)
            {
                Next();
                return;
            }

            current.SetStateDelay(StateFunction.emptyStateId, destStateId, 
                GetDelayForNode(current, index == 0));
            
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
        
        private float GetDelayForNode(BaseStateNode node, bool isFirst)
        {
            foreach (var manualNode in manualNodes)
            {
                if (manualNode.node == node && !Mathf.Approximately(manualNode.overrideDelay, dontOverrideDelay))
                    return manualNode.overrideDelay;
            }
            return isFirst ? firstDelay : perChildDelay;
        }
    }
}