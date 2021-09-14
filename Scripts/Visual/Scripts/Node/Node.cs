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

        public NodeReference referenceAsset;
        [NonSerialized]
        public NodeReference reference;
        
        [State]
        public string initialState;

        public List<Gate> gates => reference.gates;

        [SerializeField]
        public List<OutputOverride> overrides;

        // output fields of this node
        public ListMap<int, OutputField> outputFields { get; private set; } = new ListMap<int, OutputField>();
        private List<BaseField> registeredFields = new List<BaseField>(10);

        protected void OnEnable()
        {
            reference = Instantiate(referenceAsset);
            reference.name = $"{name} (Reference)";
            reference.owner = this;

            reference.Initialize();

            // subscribe to more changes
            reference.onGateAdded += RestartFields;
            reference.onGateRemoved += RestartFields;
            reference.onGatesUpdated += RestartFields;

            RestartFields();
            CacheOverrides();
        }

        protected void OnDisable()
        {
            foreach (var gate in gates.ToArray())
            {
                FinalizeGate(gate);
            }

            // unsubscribe
            reference.onGateAdded -= RestartFields;
            reference.onGateRemoved -= RestartFields;
            reference.onGatesUpdated -= RestartFields;

            Destroy(reference);
        }

        void RestartFields(Gate g) => RestartFields();
        void RestartFields()
        {
            // unregister all fields. this might be triggered by editor, so go through this list
            //. in case original serialized data had changed (instead of calling FinalizeGate(gates))
            FinalizeFields(registeredFields.ToArray());
            // re-register all gates
            foreach (var gate in gates.ToArray())  // might manipulate gates within the loop
                InitializeGate(gate);
        }

        void InitializeFields(int definitionId, IEnumerable<BaseField> fields)
        {
            // initialize all fields
            fields.ToList().ForEach(f =>
            {
                if (f == null || f is OutputField)
                    return;

                Manager.instance.RegisterField(f);

                f.Initialize(this, definitionId);
                InitializeFields(definitionId, f.GetUpstreamFields());

                AuditField(f);
            });
        }

        void FinalizeFields(IEnumerable<BaseField> fields)
        {
            // finalize all gate fields and output fields
            fields.Concat(outputFields.Values).ToList().ForEach(f =>
            {
                if (f == null || f is OutputField)
                    return;

                f.Finalize(this);
                Manager.instance?.UnregisterField(f);
                FinalizeFields(f.GetUpstreamFields());

                RemoveAudit(f);
            });
        }

        int gateIncrement;
        public void InitializeGate(Gate gate)
        {
            if (Application.isPlaying && !gate.Initialize())
                // invalid gate, don't add
                return;

            gateIncrement++;

            // make sure output field for gate is initialized
            GetOutputField(gate.outputFieldDefinitionId);

            try
            {
                InitializeFields(gate.outputFieldDefinitionId, new[] { gate.field });
            }
            catch (BaseField.FieldInitializationException)
            {
                Debug.LogWarning($"caught FieldInitializationException, removing {gate}", this);
                FinalizeGate(gate);
            }
        }
        public void FinalizeGate(Gate gate)
        {
            gateIncrement++;

            FinalizeFields(new[] { gate.field });
        }

        public OutputField GetOutputField(string name) 
            => GetOutputField(Manager.instance.GetFieldID(name));
        public OutputField GetOutputField(int fieldId)
        {
            // lazy initialization
            OutputField output;
            if (!outputFields.TryGetValue(fieldId, out output))
            {
                output = new OutputField();
                output.Initialize(this, fieldId);
                outputFields[fieldId] = output;

                AuditField(output);
            }

            return output;
        }

        private void AuditField(BaseField field)
        {
            registeredFields.Add(field);
            fieldsToNodes[field] = this;
        }
        private void RemoveAudit(BaseField field)
        {
            registeredFields.Remove(field);
            fieldsToNodes.Remove(field);
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
