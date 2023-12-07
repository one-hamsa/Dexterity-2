using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;

namespace OneHamsa.Dexterity
{
    [CreateAssetMenu(fileName = "New Node Reference", menuName = "Dexterity/Node Reference", order = 100)]
    public class NodeReference : ScriptableObject, IGateContainer, IHasStates
    {
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

            public Gate CreateDeepClone()
            {
                Gate ret = new Gate();
                ret.outputFieldName = outputFieldName;
                ret.overrideType = overrideType;
                ret.field = field.CreateDeepClone();
                ret.outputFieldDefinitionId = outputFieldDefinitionId;
                return ret;
            }

            public bool Initialize()
            {
                if (string.IsNullOrEmpty(outputFieldName))
                    return false;

                return (outputFieldDefinitionId = Database.instance.GetFieldID(outputFieldName)) != -1;
            }

            public override string ToString()
            {
                return $"{outputFieldName} Gate <{(field != null ? field.ToString() : "none")}>";
            }
        }

        public List<StateFunction> stateFunctionAssets = new();

        [SerializeField]
        public List<NodeReference> extends = new();

        [SerializeField]
        public List<Gate> gates = new();
        
        [SerializeField]
        public List<FieldDefinition> internalFieldDefinitions = new();

        [NonSerialized]
        public FieldNode owner;

        public event Action<Gate> onGateAdded;
        public event Action<Gate> onGateRemoved;
        public event Action onGatesUpdated;

        private HashSet<string> stateNames;
        private HashSet<string> fieldNames;

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
            var stateFunctionsSet = HashSetPool<StateFunction>.Get();
            try
            {
                foreach (var asset in stateFunctionAssets)
                {
                    if (asset == null)
                        continue;

                    if (stateFunctionsSet.Add(asset))
                    {
                        yield return asset;
                    }
                }

                foreach (var parent in extends)
                {
                    if (parent == null)
                        continue;

                    foreach (var asset in parent.GetStateFunctionAssetsIncludingParents())
                    {
                        if (stateFunctionsSet.Add(asset))
                        {
                            yield return asset;
                        }
                    }
                }
            }
            finally
            {
                HashSetPool<StateFunction>.Release(stateFunctionsSet);
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

        public IEnumerable<FieldDefinition> GetInternalFieldDefinitions()
        {
            foreach (var parent in extends)
            {
                if (parent == null)
                    continue;
                foreach (var field in parent.GetInternalFieldDefinitions())
                    yield return field;
            }

            foreach (var field in internalFieldDefinitions)
            {
                var f = field;
                f.isInternal = true;
                yield return f;
            }
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

        FieldNode IGateContainer.node => owner;
    }
}
