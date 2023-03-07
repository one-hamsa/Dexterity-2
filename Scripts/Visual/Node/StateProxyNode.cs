using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    [AddComponentMenu("Dexterity/State Proxy Node")]
    [DefaultExecutionOrder(Manager.nodeExecutionPriority)]
    public class StateProxyNode : DexterityBaseNode
    {
        [Serializable]
        public class StateProxy
        {
            public Node sourceNode;
            
            [State(objectFieldName: nameof(sourceNode))]
            public string inStateName;
            [NonSerialized]
            public int inStateId;

            public string outStateName;
            [NonSerialized]
            public int outStateId;
        }

        public List<StateProxy> stateProxies = new();
        public string defaultStateName = StateFunction.kDefaultState;
        private int defaultStateId;
        
        public void HandleNodesEnabled()
        {
            foreach (var stateProxy in stateProxies)
            {
                if (!stateProxy.sourceNode.isActiveAndEnabled)
                    return;
            }
            
            // all enabled
            foreach (var stateProxy in stateProxies)
            {
                stateProxy.inStateId = Core.instance.GetStateID(stateProxy.inStateName);
                if (stateProxy.inStateId == -1)
                {
                    Debug.LogError($"State {stateProxy.inStateName} not registered in Dexterity", this);
                    enabled = false;
                    return;
                }
                
                stateProxy.outStateId = Core.instance.GetStateID(stateProxy.outStateName);
                if (stateProxy.outStateId == -1)
                {
                    Debug.LogError($"State {stateProxy.outStateName} not registered in Dexterity", this);
                    enabled = false;
                    return;
                }
            }
        }

        protected override void Initialize()
        {
            foreach (var stateProxy in stateProxies)
            {
                if (stateProxy.sourceNode == null)
                {
                    Debug.LogError($"StateProxyNode {name} has a null source node for state {stateProxy.outStateName}.", this);
                    enabled = false;
                    return;
                }

                if (string.IsNullOrEmpty(stateProxy.inStateName))
                {
                    Debug.LogError($"StateProxyNode {name} has a null in state name for source node {stateProxy.sourceNode.name}.", this);
                    enabled = false;
                    return;
                }

                if (!stateProxy.sourceNode.isActiveAndEnabled)
                {
                    stateProxy.sourceNode.onEnabled -= HandleNodesEnabled;
                    stateProxy.sourceNode.onEnabled += HandleNodesEnabled;
                }
            }

            base.Initialize();
            HandleNodesEnabled();
            
            defaultStateId = Core.instance.GetStateID(defaultStateName);
        }
        
        protected override void UpdateInternal(bool ignoreDelays)
        {
            // since this type of node is using a data source, state should always be considered dirty
            // XXX optimization: listen to nodes' onStateChange events and only set dirty when necessary
            stateDirty = true;
            
            base.UpdateInternal(ignoreDelays);
        }
        
        protected override int GetState()
        {
            var baseState = base.GetState();
            if (baseState != StateFunction.emptyStateId)
                return baseState;

            foreach (var stateProxy in stateProxies)
            {
                if (stateProxy.sourceNode.GetActiveState() == stateProxy.inStateId)
                    return stateProxy.outStateId;
            }

            return defaultStateId;
        }

        public override IEnumerable<string> GetStateNames()
        {
            foreach (var stateProxy in stateProxies)
            {
                yield return stateProxy.outStateName;
            }
            yield return defaultStateName;
        }

        public override IEnumerable<string> GetFieldNames() => Enumerable.Empty<string>();
    }
}
