using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using OneHumus.Data;

namespace OneHamsa.Dexterity.Visual
{
    using Gate = NodeReference.Gate;

    [AddComponentMenu("Dexterity/Node")]
    [DefaultExecutionOrder(Manager.nodeExecutionPriority)]
    public partial class Node : MonoBehaviour
    {
        // mainly for debugging graph problems
        private static ListMap<BaseField, Node> fieldsToNodes = new ListMap<BaseField, Node>();
        internal static Node ByField(BaseField f)
        {
            fieldsToNodes.TryGetValue(f, out var node);
            return node;
        }

        public NodeReference reference;
        
        public string initialState;

        [NonSerialized]
        public List<Gate> gates = new List<Gate>(8);

        [Serializable]
        public class OutputOverride
        {
            [Field]
            public string outputFieldName;
            [FieldValue(nameof(outputFieldName), proxy = true)]
            public int value;

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
        }

        [SerializeField]
        public List<OutputOverride> overrides;

        private void LoadFromReference()
        {
            gates.Clear();

            foreach (var gate in reference.gates)
                gates.Add(gate);
        }

        protected void OnEnable()
        {
            LoadFromReference();

            foreach (var gate in gates.ToArray())  // might manipulate gates within the loop
            {
                if (!gate.Initialize())
                {
                    Debug.LogWarning($"Removing invalid gate {gate}", this);
                    RemoveGate(gate);
                    continue;
                }
                // initialize 
                InitializeFields(gate, new BaseField[] { gate.field });
            }

            CacheOverrides();
        }

        protected void OnDisable()
        {
            foreach (var gate in gates.ToArray())
            {
                FinalizeFields(gate, new BaseField[] { gate.field });
            }
        }

        void InitializeFields(Gate gate, IEnumerable<BaseField> fields)
        {
            // make sure output field for gate is initialized
            GetOutputField(gate.outputFieldDefinitionId);

            // initialize all fields
            fields.ToList().ForEach(f =>
            {
                if (f == null || f is OutputField)
                    return;

                Manager.instance.RegisterField(f);
                try
                {
                    f.Initialize(this);
                    InitializeFields(gate, f.GetUpstreamFields());

                    fieldsToNodes[f] = this;
                }
                catch (BaseField.FieldInitializationException)
                {
                    Debug.LogWarning($"caught FieldInitializationException, removing {gate}", this);
                    RemoveGate(gate);
                    return;
                }
            });
        }

        void FinalizeFields(Gate gate, IEnumerable<BaseField> fields)
        {
            // finalize all gate fields and output fields
            fields.Concat(outputFields.Values).ToList().ForEach(f =>
            {
                if (f == null || f is OutputField)
                    return;

                f.Finalize(this);
                Manager.instance?.UnregisterField(f);
                FinalizeFields(gate, f.GetUpstreamFields());

                fieldsToNodes.Remove(f);
            });
        }

        int gateIncrement;
        public void AddGate(Gate gate)
        {
            gateIncrement++;
            gates.Add(gate);

            InitializeFields(gate, new[] { gate.field });
        }
        public void RemoveGate(Gate gate)
        {
            gateIncrement++;
            gates.Remove(gate);
            FinalizeFields(gate, new[] { gate.field });
        }

        // output fields of this node
        public ListMap<int, OutputField> outputFields { get; private set; } = new ListMap<int, OutputField>();

        public OutputField GetOutputField(string name) 
            => GetOutputField(Manager.instance.GetFieldID(name));
        public OutputField GetOutputField(int fieldId)
        {
            // lazy initialization
            OutputField output;
            if (!outputFields.TryGetValue(fieldId, out output))
            {
                output = new OutputField(fieldId);
                output.Initialize(this);
                outputFields[fieldId] = output;

                fieldsToNodes[output] = this;
            }

            return output;
        }

        int overridesIncrement;
        public ListMap<int, OutputOverride> cachedOverrides { get; private set; } = new ListMap<int, OutputOverride>();
        public void SetOverride(int fieldId, int value)
        {
            if (!cachedOverrides.ContainsKey(fieldId))
            {
                var newOverride = new OutputOverride();
                newOverride.Initialize(fieldId);

                overrides.Add(newOverride);
                CacheOverrides();
            }
            var overrideOutput = cachedOverrides[fieldId];
            overrideOutput.value = value;
        }
        public void ClearOverride(int fieldId)
        {
            if (cachedOverrides.ContainsKey(fieldId))
            {
                overrides.Remove(cachedOverrides[fieldId]);
                CacheOverrides();
            }
            else
            {
                Debug.LogWarning($"clearing undefined override {name}");
            }
        }

        void CacheOverrides()
        {
            cachedOverrides.Clear();
            foreach (var o in overrides)
            {
                o.Initialize();
                cachedOverrides[o.outputFieldDefinitionId] = o;
            }
            overridesIncrement++;
        }

#if UNITY_EDITOR
        // update overrides every frame to allow setting overrides from editor
        void LateUpdate()
        {
            CacheOverrides();
        }
#endif
    }
}
