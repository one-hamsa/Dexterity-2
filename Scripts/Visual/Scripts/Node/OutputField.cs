using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    public partial class Node
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
            public readonly string name;
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

                    if (!node.cachedOverrides.TryGetValue(name, out cachedOverride))
                        cachedOverride = null;
                    overridesIncrement = node.overridesIncrement;
                    return cachedOverride;
                }
            }

            public OutputField(string name)
            {
                this.name = name;
            }
            public override void Initialize(Node context)
            {
                base.Initialize(context);
                node = context;
                Manager.Instance.RegisterField(this);
                definition = Manager.Instance.GetFieldDefinition(name).Value;                
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
                    if (gate.outputFieldName != name)
                        continue;

                    // XXX could possibly cache each gate field independently
                    allUpstreamFieldsAreOutputFields &= IsAllUpstreamProxyOrOutput(gate.field);

                    cachedGates.Add(gate);
                    AddUpstreamField(gate.field);
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
                        cachedValueWithoutOverride = cachedGates.Last().field.GetValue();
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
                    cachedValue = outputOverride.value;
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

                return $"OutputNode {node.name}::{name} -> {GetValue()}";
            }
        }
    }
}
