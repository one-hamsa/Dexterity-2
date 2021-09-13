using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using OneHumus.Data;

namespace OneHamsa.Dexterity.Visual
{
    [CreateAssetMenu(fileName = "New Node Reference", menuName = "Dexterity/Node Reference", order = 100)]
    public class NodeReference : ScriptableObject
    {
        // stores the coupling between input fields and their output name
        [Serializable]
        public class Gate
        {
            [Field]
            public string outputFieldName;

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
        public StateFunctionGraph stateFunction;

        [SerializeField]
        public List<Gate> gates;

        [SerializeField]
        public List<TransitionDelay> delays;

        [HideInInspector]
        public string defaultStrategy;

        [NonSerialized]
        public Node owner;

        public event Action<Gate> onGateAdded;
        public event Action<Gate> onGateRemoved;
        public event Action onGatesUpdated;

        ListMap<int, TransitionDelay> cachedDelays;

        public void Initialize()
        {
            // cache delays
            cachedDelays = new ListMap<int, TransitionDelay>();
            foreach (var delay in delays)
                cachedDelays.Add(Manager.instance.GetStateID(delay.state), delay);
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
    }
}
