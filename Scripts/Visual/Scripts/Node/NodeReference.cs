using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    [CreateAssetMenu(fileName = "New Node Reference", menuName = "Dexterity/Node Reference", order = 100)]
    public class NodeReference : ScriptableObject, IGateContainer, IStatesProvider
    {
        private static Dictionary<NodeReference, NodeReference> prefabToRuntime
            = new Dictionary<NodeReference, NodeReference>();

        // stores the coupling between input fields and their output name
        [Serializable]
        public class Gate
        {
            public enum OverrideType {
                Additive = 1,
                Subtractive = 2,
                Always = Additive | Subtractive,
            }

            [Field]
            public string outputFieldName;
            public OverrideType overrideType = OverrideType.Additive;

            [SerializeReference]
            public BaseField field;

            public int outputFieldDefinitionId { get; private set; } = -1;

            public bool Initialize()
            {
                if (string.IsNullOrEmpty(outputFieldName))
                    return false;

                return (outputFieldDefinitionId = Manager.instance.GetFieldID(outputFieldName)) != -1;
            }

            public override string ToString()
            {
                return $"{outputFieldName} Gate <{(field != null ? field.ToString() : "none")}>";
            }
        }

        [Serializable]
        public class TransitionDelay
        {
            [State]
            public string state;
            public float delay = 0;
        }

        public List<StateFunctionGraph> stateFunctionAssets = new List<StateFunctionGraph>();

        [SerializeField]
        public List<NodeReference> extends = new List<NodeReference>();

        [SerializeField]
        public List<Gate> gates = new List<Gate>();

        [SerializeField]
        public List<TransitionDelay> delays = new List<TransitionDelay>();

        [NonSerialized]
        public Node owner;

        public StateFunctionGraph[] stateFunctions { get; private set; }

        public event Action<Gate> onGateAdded;
        public event Action<Gate> onGateRemoved;
        public event Action onGatesUpdated;

        Dictionary<int, TransitionDelay> cachedDelays;
        HashSet<StateFunctionGraph> stateFunctionsSet = new HashSet<StateFunctionGraph>();
        private int defaultStateId = -1;

        private static HashSet<NodeReference> parentReferences = new HashSet<NodeReference>();

        public void Initialize(IEnumerable<Gate> gates, HashSet<NodeReference> parentReferences = null)
        {
            if (parentReferences == null) {
                parentReferences = NodeReference.parentReferences;
                parentReferences.Clear();
            }

            // register and initialize all functions
            var assets = GetStateFunctionAssetsIncludingParents().ToList();
            stateFunctions = new StateFunctionGraph[assets.Count];
            for (int i = 0; i < assets.Count; i++)
            {
                stateFunctions[i] = Manager.instance.RegisterStateFunction(assets[i]);
            }

            // cache default state
            defaultStateId = Manager.instance.GetStateID(StateFunctionGraph.kDefaultState);

            // copy from parents
            foreach (var parent in extends)
            {
                // skip if already added 
                if (!parentReferences.Add(parent))
                    continue;

                // deep clone before iterating gates to make sure we point to new instances
                var newParent = Instantiate(parent);

                // make sure it's recursive
                newParent.Initialize(new Gate[] { }, parentReferences);
                
                var i = 0;
                foreach (var gate in newParent.gates)
                {
                    this.gates.Insert(i++, gate);
                }

                i = 0;
                foreach (var delay in newParent.delays)
                {
                    delays.Insert(i++, delay);
                }

                // c'est tout
                Destroy(newParent);
            }

            // add new gates
            foreach (var gate in gates)
            {
                this.gates.Add(gate);
            }

            // cache delays
            cachedDelays = new Dictionary<int, TransitionDelay>();
            foreach (var delay in delays)
                cachedDelays[Manager.instance.GetStateID(delay.state)] = delay;
        }

        public TransitionDelay GetDelay(int state)
        {
            cachedDelays.TryGetValue(state, out var value);
            return value;
        }

        public void AddGate(Gate gate)
        {
            gates.Add(gate);
            onGateAdded?.Invoke(gate);
        }

        public void RemoveGate(Gate gate)
        {
            gates.Remove(gate);
            onGateRemoved?.Invoke(gate);
        }

        public void NotifyGatesUpdate()
        {
            onGatesUpdated?.Invoke();
        }

        // interface implementations
        public int GetGateCount() => gates.Count;
        public Gate GetGateAtIndex(int i)
        {
            return gates[i];
        }

        public IEnumerable<StateFunctionGraph> GetStateFunctionAssetsIncludingParents() {
            stateFunctionsSet.Clear();
            foreach (var asset in stateFunctionAssets) {
                if (asset == null)
                    continue;

                if (stateFunctionsSet.Add(asset)) {
                    yield return asset;
                }
            }
            foreach (var parent in extends) {
                if (parent == null)
                    continue;

                foreach (var asset in parent.GetStateFunctionAssetsIncludingParents()) {
                    if (stateFunctionsSet.Add(asset)) {
                        yield return asset;
                    }
                }
            }
        }

        public IEnumerable<string> GetStateNames()
        => StateFunctionGraph.EnumerateStateNames(GetStateFunctionAssetsIncludingParents());

        public IEnumerable<string> GetFieldNames()
        => StateFunctionGraph.EnumerateFieldNames(GetStateFunctionAssetsIncludingParents());

        public IEnumerable<int> GetFieldIDs()
        {
            foreach (var name in GetFieldNames())
            {
                yield return Manager.instance.GetFieldID(name);
            }
        }
        public IEnumerable<int> GetStateIDs()
        {
            foreach (var stateName in GetStateNames())
                yield return Manager.instance.GetStateID(stateName);
        }

        internal int Evaluate(FieldsState fieldsState)
        {
            foreach (var function in stateFunctions) {
                var result = function.Evaluate(fieldsState);
                if (result != -1)
                    return result;
            }
            return defaultStateId;
        }

        private void OnValidate() {
            // add all state functions from references
            foreach (var reference in extends) {
                if (reference == null)
                    continue;

                foreach (var asset in reference.GetStateFunctionAssetsIncludingParents()) {
                    if (!stateFunctionAssets.Contains(asset)) {
                        stateFunctionAssets.Add(asset);
                    }
                }
            }
        }

        IEnumerable<string> IGateContainer.GetStateNames() => (this as IStatesProvider).GetStateNames();
        IEnumerable<string> IGateContainer.GetFieldNames() => (this as IStatesProvider).GetFieldNames();

        Node IGateContainer.node => owner;
    }
}
