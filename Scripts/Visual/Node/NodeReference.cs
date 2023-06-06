using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    [CreateAssetMenu(fileName = "New Node Reference", menuName = "Dexterity/Node Reference", order = 100)]
    public class NodeReference : ScriptableObject, IGateContainer, IHasStates
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

                return (outputFieldDefinitionId = Core.instance.GetFieldID(outputFieldName)) != -1;
            }

            public override string ToString()
            {
                return $"{outputFieldName} Gate <{(field != null ? field.ToString() : "none")}>";
            }
        }

        public List<StateFunction> stateFunctionAssets = new List<StateFunction>();

        [SerializeField]
        public List<NodeReference> extends = new List<NodeReference>();

        [SerializeField]
        public List<Gate> gates = new List<Gate>();


        [NonSerialized]
        public Node owner;

        public StateFunction[] stateFunctions { get; private set; }

        public event Action<Gate> onGateAdded;
        public event Action<Gate> onGateRemoved;
        public event Action onGatesUpdated;

        HashSet<StateFunction> stateFunctionsSet = new HashSet<StateFunction>();

        private static HashSet<NodeReference> parentReferences = new HashSet<NodeReference>();
        private HashSet<string> stateNames;
        private HashSet<string> fieldNames;

        public void Initialize(IEnumerable<Gate> gates, HashSet<NodeReference> parentReferences = null)
        {
            if (parentReferences == null) {
                parentReferences = NodeReference.parentReferences;
                parentReferences.Clear();
            }

            // register all functions
            stateFunctions = GetStateFunctionAssetsIncludingParents().ToArray();
            for (int i = 0; i < stateFunctions.Length; i++) {
                Core.instance.Register(stateFunctions[i]);
            }

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

                // c'est tout
                Destroy(newParent);
            }

            // add new gates
            foreach (var gate in gates)
            {
                this.gates.Add(gate);
            }
        }

        public void Uninitialize()
        {
            gates.Clear();
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

        public IEnumerable<StateFunction> GetStateFunctionAssetsIncludingParents() {
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

        public HashSet<string> GetStateNames()
        {
            stateNames ??= StateFunction.EnumerateStateNames(GetStateFunctionAssetsIncludingParents()).ToHashSet();
            return stateNames;
        }

        public HashSet<string> GetFieldNames()
        {
            fieldNames ??= StateFunction.EnumerateFieldNames(GetStateFunctionAssetsIncludingParents()).ToHashSet();
            return fieldNames;
        }

        internal int Evaluate(FieldMask fieldMask)
        {
            foreach (var function in stateFunctions) {
                var result = function.Evaluate(fieldMask);
                if (result != StateFunction.emptyStateId)
                    return result;
            }
            return StateFunction.emptyStateId;
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

        IEnumerable<string> IGateContainer.GetStateNames() => (this as IHasStates).GetStateNames();
        IEnumerable<string> IGateContainer.GetFieldNames() => (this as IHasStates).GetFieldNames();

        Node IGateContainer.node => owner;
    }
}
