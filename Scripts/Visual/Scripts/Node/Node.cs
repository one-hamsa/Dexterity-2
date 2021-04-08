using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    [AddComponentMenu("Dexterity/Visual/Dexterity Visual - Node")]
    [DefaultExecutionOrder(Manager.NodeExecutionPriority)]
    public class Node : MonoBehaviour
    {
        public const int EMPTY_FIELD_VALUE = -1;
        public const int DEFAULT_FIELD_VALUE = 0;

        // differentiates between field types. the underlying data is always int, but the way
        //. the system treats this data can vary.
        public enum FieldType
        {
            Boolean = 1,
            Enum = 2,
        }

        // stores outputs for the current node
        public class OutputField : BaseField
        {
            // hide
            public static new bool ShowInInspector = false;

            Node node;
            public readonly string Name;
            protected int cachedValue = EMPTY_FIELD_VALUE;
            protected int cachedValueWithoutOverride = EMPTY_FIELD_VALUE;
            protected Manager.FieldDefinition definition;

            // optimizations
            int gateIncrement = -1;
            bool allUpstreamFieldsAreOutputFields;
            bool areUpstreamOutputFieldsDirty;
            protected List<Gate> cachedGates = new List<Gate>();
            OutputOverride cachedOverride = null;
            int overridesIncrement = -1;

            /// register to events like that:
            /// <code>node.GetOutputField("fieldName").OnValueChanged += HandleValueChanged;</code>
            public event Action<OutputField, int, int> OnValueChanged;

            protected OutputOverride Override
            {
                get 
                {
                    if (overridesIncrement == node.overridesIncrement)
                        return cachedOverride;

                    if (!node.cachedOverrides.TryGetValue(Name, out cachedOverride))
                        cachedOverride = null;
                    overridesIncrement = node.overridesIncrement;
                    return cachedOverride;
                }
            }

            public OutputField(string name)
            {
                Name = name;
            }
            public override void Initialize(Node context)
            {
                base.Initialize(context);
                node = context;
                Manager.Instance.RegisterField(this);
                definition = Manager.Instance.GetFieldDefinition(Name).Value;                
            }
            public override void Finalize(Node context)
            {
                base.Finalize(context);
                Manager.Instance?.UnregisterField(this);
            }

            public override void RefreshReferences()
            {
                if (gateIncrement == node.gateIncrement)
                    return;

                cachedGates.Clear();

                if (allUpstreamFieldsAreOutputFields)
                {
                    // clear update subscriptions
                    foreach (var field in GetUpstreamFields())
                    {
                        UnregisterUpstreamOutput(field);
                    }
                }

                ClearUpstreamFields();
                allUpstreamFieldsAreOutputFields = true;
                foreach (var gate in node.gates)
                {
                    if (gate.OutputFieldName != Name)
                        continue;

                    // XXX could possibly cache each gate field independently
                    allUpstreamFieldsAreOutputFields &= IsAllUpstreamProxyOrOutput(gate.Field);

                    cachedGates.Add(gate);
                    AddUpstreamField(gate.Field);
                }
                gateIncrement = node.gateIncrement;

                if (allUpstreamFieldsAreOutputFields)
                {
                    areUpstreamOutputFieldsDirty = true;
                    // we can just register to the output fields changes
                    foreach (var field in GetUpstreamFields())
                    {
                        RegisterUpstreamOutput(field);
                    }
                }
            }

            private bool IsAllUpstreamProxyOrOutput(BaseField field)
            {
                if (!(field is OutputField) && !field.isProxy)
                    return false;

                bool proxyOrOutput = true;
                if (field.isProxy)
                    foreach (var f in field.GetUpstreamFields())
                        proxyOrOutput &= IsAllUpstreamProxyOrOutput(f);
                return proxyOrOutput;
            }
            private void RegisterUpstreamOutput(BaseField field)
            {
                if (field is OutputField)
                    (field as OutputField).OnValueChanged += UpstreamOutputChanged;
                else if (field.isProxy)
                    foreach (var f in field.GetUpstreamFields())
                        RegisterUpstreamOutput(f);
            }
            private void UnregisterUpstreamOutput(BaseField field)
            {
                if (field is OutputField)
                    (field as OutputField).OnValueChanged -= UpstreamOutputChanged;
                else if (field.isProxy)
                    foreach (var f in field.GetUpstreamFields())
                        UnregisterUpstreamOutput(f);
            }

            private void UpstreamOutputChanged(OutputField field, int oldValue, int newValue)
            {
                // whatever the new value is, just mark as dirty
                areUpstreamOutputFieldsDirty = true;
            }

            public override int GetValue()
            {
                return cachedValue;
            }

            public override void CacheValue()
            {
                var originalValue = cachedValue;
                if (!allUpstreamFieldsAreOutputFields || areUpstreamOutputFieldsDirty)
                {
                    // merge it with the other gate according to the field's type
                    if (definition.Type == FieldType.Boolean)
                    {
                        // additive: 1 if any is 1, 0 otherwise
                        var result = false;
                        foreach (var field in GetUpstreamFields())
                        {
                            result |= field.GetValue() == 1;
                            if (result)
                                break;
                        }

                        cachedValueWithoutOverride = result ? 1 : 0;
                    }
                    else if (definition.Type == FieldType.Enum)
                    { 
                        // override: take last one
                        cachedValueWithoutOverride = cachedGates.Last().Field.GetValue();
                    }
                }
                else
                {
                    // all fields are output fields and no upstream field changed - no need to update
                }

                // if there's an override, just use it
                var outputOverride = Override;
                if (outputOverride != null) 
                {
                    cachedValue = outputOverride.Value;
                }
                else
                {
                    cachedValue = cachedValueWithoutOverride;
                }

                if (allUpstreamFieldsAreOutputFields)
                    areUpstreamOutputFieldsDirty = false;

                // notify if value changed
                if (cachedValue != originalValue)
                    OnValueChanged?.Invoke(this, originalValue, cachedValue);
            }

            // for debug purposes only, mostly for editor view (to avoid masking the original field value)
            public int GetValueWithoutOverride()
            {
                return cachedValueWithoutOverride;
            }

            public override string ToString()
            {
                if (!node)
                    return base.ToString();

                return $"OutputNode {node.name}::{Name} -> {GetValue()}";
            }
        }

        // stores the coupling between input fields and their output name
        [Serializable]
        public class Gate
        {
            public string OutputFieldName;

            [SerializeReference]
            public BaseField Field;
        }

        [SerializeField]
        protected List<Gate> gates;

        [Serializable]
        public class OutputOverride
        {
            public string OutputFieldName;
            public int Value;
        }

        [SerializeField]
        protected List<OutputOverride> overrides;

        protected void OnEnable()
        {
            InitializeFields(gates.Select(g => g.Field));
            CacheOverrides();
        }
        protected void OnDisable()
        {
            FinalizeFields(gates.Select(g => g.Field));
        }

        void InitializeFields(IEnumerable<BaseField> fields)
        {
            // initialize all fields
            fields.ToList().ForEach(f =>
            {
                if (f is OutputField)
                    return;

                Manager.Instance.RegisterField(f);
                f.Initialize(this);
                InitializeFields(f.GetUpstreamFields());
            });
        }

        void FinalizeFields(IEnumerable<BaseField> fields)
        {
            // finalize all gate fields and output fields
            fields.Concat(outputFields.Values).ToList().ForEach(f =>
            {
                if (f is OutputField)
                    return;

                f.Finalize(this);
                Manager.Instance?.UnregisterField(f);
                FinalizeFields(f.GetUpstreamFields());
            });
        }

        public List<Gate> Gates => gates;
        int gateIncrement;
        public void AddGate(Gate gate)
        {
            gateIncrement++;
            gates.Add(gate);
            InitializeFields(new[] { gate.Field });
        }
        public void RemoveGate(Gate gate)
        {
            gateIncrement++;
            gates.Remove(gate);
            FinalizeFields(new[] { gate.Field });
        }

        // output fields of this node
        protected Dictionary<string, OutputField> outputFields = new Dictionary<string, OutputField>();
        public Dictionary<string, OutputField> GetOutputFields() => outputFields;
        public OutputField GetOutputField(string name)
        {
            // lazy initialization
            OutputField output;
            if (!outputFields.TryGetValue(name, out output))
            {
                output = new OutputField(name);
                output.Initialize(this);
                outputFields[name] = output;
            }

            return output;
        }

        int overridesIncrement;
        Dictionary<string, OutputOverride> cachedOverrides = new Dictionary<string, OutputOverride>();
        public Dictionary<string, OutputOverride> GetOverrides() => cachedOverrides;
        public void SetOverride(string name, int value)
        {
            if (!cachedOverrides.ContainsKey(name))
            {
                overrides.Add(new OutputOverride { OutputFieldName = name });
                CacheOverrides();
            }
            var overrideOutput = cachedOverrides[name];
            overrideOutput.Value = value;
        }
        public void ClearOverride(string name)
        {
            if (cachedOverrides.ContainsKey(name))
            {
                overrides.Remove(cachedOverrides[name]);
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
                cachedOverrides[o.OutputFieldName] = o;
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
