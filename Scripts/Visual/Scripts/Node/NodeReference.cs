using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    [CreateAssetMenu(fileName = "New Node Reference", menuName = "Dexterity/Node Reference", order = 100)]
    public class NodeReference : ScriptableObject, IGateContainer
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
                All = Additive | Subtractive,
            }

            [Field]
            public string outputFieldName;
            public OverrideType overrideType = OverrideType.Additive;

            [SerializeReference]
            public BaseField field;

            public int outputFieldDefinitionId { get; private set; } = -1;

            public bool Initialize(int fieldId = -1)
            {
                if (fieldId != -1)
                {
                    outputFieldDefinitionId = fieldId;
                    return true;
                }
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

        [SerializeField]
        public StateFunctionGraph stateFunctionAsset;

        [SerializeField]
        public List<NodeReference> extends = new List<NodeReference>();

        [SerializeField]
        public List<Gate> gates = new List<Gate>();

        [SerializeField]
        public List<TransitionDelay> delays = new List<TransitionDelay>();

        [NonSerialized]
        public Node owner;

        public StateFunctionGraph stateFunction { get; private set; }

        public event Action<Gate> onGateAdded;
        public event Action<Gate> onGateRemoved;
        public event Action onGatesUpdated;

        Dictionary<int, TransitionDelay> cachedDelays;

        public void Initialize(IEnumerable<Gate> gates)
        {
            stateFunction = stateFunctionAsset.GetRuntimeInstance();

            // copy from parents
            foreach (var parent in extends)
            {
                // deep clone before iterating gates to make sure we point to new instances
                var newParent = Instantiate(parent);

                // make sure it's recursive
                newParent.Initialize(new Gate[] { });
                
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
        public Gate GetGateAtIndex(int i)
        {
            return gates[i];
        }
        StateFunctionGraph IGateContainer.stateFunctionAsset => stateFunctionAsset;
        Node IGateContainer.node => owner;
    }
}
