using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    using Gate = NodeReference.Gate;

    public partial class Node
    {
        public const int emptyFieldValue = -1;
        public const int defaultFieldValue = 0;

        // differentiates between field types. the underlying data is always int, but the way
        //. the system treats this data can vary.
        public enum FieldType
        {
            Boolean = 1,
            Enum = 2,
        }

        /// <summary>
        /// Output Field stores field outputs for nodes and allows easy API access to the field status.
        /// </summary>
        public class OutputField : BaseField
        {
            public Node node { get; private set; }
            public string originalNodeName { get; private set; }

            protected int cachedValue = emptyFieldValue;
            protected int cachedValueWithoutOverride = emptyFieldValue;

            #region Optimization Fields
            // tracks the last gates dirty increment from the node, if it's different we need to update
            int gatesDirtyIncrement = -1;
            // marks if the output node is dependent only on other output nodes (or proxies for outputs)
            bool allUpstreamFieldsAreOutputOrProxy;
            // in case the output node is dependent only on other output nodes, tracks if those are dirty
            bool areUpstreamOutputOrProxyFieldsDirty;
            // saves a list of all gates of this output field
            protected List<Gate> cachedGates = new List<Gate>();
            // saves a list of all gates of last reference update
            protected List<Gate> prevCachedGates = new List<Gate>();
            // saves a reference to an override to avoid unnecessary lookup
            OutputOverride cachedOverride = null;
            // tracks the last overrides dirty increment from the node, if it's different we need to update
            int overridesDirtyIncrement = -1;
            #endregion

            /// <summary>
            /// Invoked when output's value is changed.
            /// Register to events like that:
            /// <code>node.GetOutputField("fieldName").OnValueChanged += HandleValueChanged;</code>
            /// </summary>
            public event Action<OutputField, int, int> onValueChanged;

            /// <summary>
            /// Invoked when output's value is changed, only fired for boolean types.
            /// </summary>
            public event Action<OutputField, bool, bool> onBooleanValueChanged;

            /// <summary>
            /// Invoked when output's value is changed, only fired for enum types.
            /// </summary>
            public event Action<OutputField, string, string> onEnumValueChanged;

            private bool registered;

            protected OutputOverride fieldOverride
            {
                get 
                {
                    if (overridesDirtyIncrement == node.overridesDirtyIncrement)
                        return cachedOverride;

                    if (!node.cachedOverrides.TryGetValue(definitionId, out cachedOverride))
                        cachedOverride = null;
                    overridesDirtyIncrement = node.overridesDirtyIncrement;
                    return cachedOverride;
                }
            }
            protected override void Initialize(Node context)
            {
                base.Initialize(context);

                // save reference to node
                node = context;
                originalNodeName = node.name;
                
                node.onEnabled += OnNodeEnabled;
                node.onDisabled += OnNodeDisabled;
                
                if (node.isActiveAndEnabled)
                    OnNodeEnabled();
            }

            public override void Finalize(Node context)
            {
                base.Finalize(context);

                // notify that the output field's value is no longer used
                InvokeEvents(cachedValue, defaultFieldValue);
                
                if (Manager.instance != null && registered)
                    // unregister this field from manager
                    Manager.instance.UnregisterField(this);

                if (node != null)
                {
                    node.onEnabled -= OnNodeEnabled;
                    node.onDisabled -= OnNodeDisabled;
                }
                fieldsToNodes.Remove(this);
            }

            private void OnNodeEnabled()
            {
                if (!registered)
                {
                    // register this field in manager to get updates from graph
                    Manager.instance.RegisterField(this);
                    fieldsToNodes[this] = node;
                    registered = true;
                }

                // if the node is enabled, we might need to update the value
                CacheValue();
            }
            
            private void OnNodeDisabled()
            {
                // if the node is disabled, we might need to reset the value
                CacheValue();
            }

            /// <summary>
            /// refreshes the list of gates of this output field, and marks for optimizations.
            /// for instance, if the output field is dependent only on other output fields,
            /// this will be marked by this method so that we can avoid recalculating the output value
            /// every graph update cycle (and instead track the changes using output field events).
            ///
            /// NOTE: unfortunately this optimization cannot be (easily) implemented on any field, 
            /// since fields can have recursive references to other fields, so tracking the changes
            /// of those fields might be very tricky. instead, we ask for the field's GetValue()
            /// on our CacheValue() (if the optimization is not possible).
            /// </summary>
            public override void RefreshReferences()
            {
                // only update if something changed in the node's gates
                if (gatesDirtyIncrement == node.gatesDirtyIncrement)
                    return;

                prevCachedGates.Clear();
                prevCachedGates.AddRange(cachedGates);
                cachedGates.Clear();

                // clear previous update subscriptions if needed
                if (allUpstreamFieldsAreOutputOrProxy)
                {
                    foreach (var field in GetUpstreamFields())
                    {
                        UnregisterUpstreamOutput(field);
                    }
                }

                // find whether this output field is dependent only on other output fields
                allUpstreamFieldsAreOutputOrProxy = true;
                foreach (var gate in node.allGates)
                {
                    if (gate.outputFieldDefinitionId != definitionId || gate.field == null)
                        continue;

                    // XXX could possibly cache each gate field independently
                    allUpstreamFieldsAreOutputOrProxy &= IsAllUpstreamProxyOrOutput(gate.field);

                    cachedGates.Add(gate);
                }

                // compare and change upstream if needed
                var needsUpdate = true;
                if (cachedGates.Count == prevCachedGates.Count) {
                    needsUpdate = false;
                    for (var i = 0; i < cachedGates.Count; i++)
                    {
                        if (cachedGates[i] != prevCachedGates[i])
                        {
                            needsUpdate = true;
                            break;
                        }
                    }
                }
                if (needsUpdate) {
                    ClearUpstreamFields();
                    foreach (var gate in cachedGates)
                    {
                        AddUpstreamField(gate.field);
                    }
                }

                // we just finished refreshing our references, update the tracked dirty increment
                gatesDirtyIncrement = node.gatesDirtyIncrement;

                // subscribe to changes in output fields if needed
                if (allUpstreamFieldsAreOutputOrProxy)
                {
                    areUpstreamOutputOrProxyFieldsDirty = true;
                    foreach (var field in GetUpstreamFields())
                    {
                        RegisterUpstreamOutput(field);
                    }
                }
            }

            public override void RebuildCache()
            {
                gatesDirtyIncrement = -1;
                overridesDirtyIncrement = -1;
            }

            private void InvokeEvents(int oldValue, int newValue)
            {
                onValueChanged?.Invoke(this, oldValue, newValue);
                switch (definition.type)
                {
                    case FieldType.Boolean:
                        onBooleanValueChanged?.Invoke(this, oldValue == 1, newValue == 1);
                        break;
                    case FieldType.Enum:
                        onEnumValueChanged?.Invoke(this, definition.enumValues[oldValue], definition.enumValues[newValue]);
                        break;
                }
            }

            private bool IsAllUpstreamProxyOrOutput(BaseField field)
            {
                if (!(field is OutputField) && !field.proxy)
                    return false;

                bool proxyOrOutput = true;
                if (field.proxy)
                    foreach (var f in field.GetUpstreamFields())
                        proxyOrOutput &= IsAllUpstreamProxyOrOutput(f);
                return proxyOrOutput;
            }
            private void RegisterUpstreamOutput(BaseField field)
            {
                if (field is OutputField)
                    (field as OutputField).onValueChanged += UpstreamOutputChanged;
                else if (field.proxy)
                    foreach (var f in field.GetUpstreamFields())
                        RegisterUpstreamOutput(f);
            }
            private void UnregisterUpstreamOutput(BaseField field)
            {
                if (field is OutputField)
                    (field as OutputField).onValueChanged -= UpstreamOutputChanged;
                else if (field.proxy)
                    foreach (var f in field.GetUpstreamFields())
                        UnregisterUpstreamOutput(f);
            }

            private void UpstreamOutputChanged(OutputField field, int oldValue, int newValue)
            {
                // whatever the new value is, just mark as dirty
                areUpstreamOutputOrProxyFieldsDirty = true;
            }

            public override int GetValue()
            {
                return cachedValue;
            }

            public override void CacheValue()
            {
                var originalValue = cachedValue;
                if (node == null || !node.isActiveAndEnabled)
                {
                    // if the node is disabled, it's always 0
                    cachedValueWithoutOverride = 0;
                }
                else if (!allUpstreamFieldsAreOutputOrProxy || areUpstreamOutputOrProxyFieldsDirty)
                {
                    // merge it with the other gate according to the field's type
                    if (definition.type == FieldType.Boolean)
                    {
                        var result = false;
                        foreach (var gate in cachedGates)
                        {
                            var found = false;

                            if (gate.overrideType.HasFlag(Gate.OverrideType.Additive)) {
                                result |= gate.field.GetBooleanValue();
                                found = true;
                            }
                            if (gate.overrideType.HasFlag(Gate.OverrideType.Subtractive)) {
                                result &= gate.field.GetBooleanValue();
                                found = true;
                            }

                            if (!found)
                                Debug.LogError($"Unknown override type {gate.overrideType} for field {gate.field.definition.name}", node);
                        }

                        cachedValueWithoutOverride = result ? 1 : 0;
                    }
                    else if (definition.type == FieldType.Enum)
                    {
                        if (cachedGates.Count > 0)
                            // override: take last one
                            cachedValueWithoutOverride = cachedGates[cachedGates.Count - 1].field.GetValue();
                        else
                            // default enum value is first value
                            cachedValueWithoutOverride = 0;
                    }
                }
                else
                {
                    // all fields are output fields and no upstream field changed - no need to update
                }

                // if there's an override, just use it
                var outputOverride = fieldOverride;
                if (outputOverride != null && node != null && node.isActiveAndEnabled) 
                {
                    cachedValue = outputOverride.value;
                }
                else
                {
                    cachedValue = cachedValueWithoutOverride;
                }

                if (allUpstreamFieldsAreOutputOrProxy)
                    areUpstreamOutputOrProxyFieldsDirty = false;

                // notify if value changed
                if (cachedValue != originalValue)
                    InvokeEvents(originalValue, cachedValue);
            }

            // for debug purposes only, mostly for editor view (to avoid masking the original field value)
            public int GetValueWithoutOverride()
            {
                return cachedValueWithoutOverride;
            }
            
            public override string ToShortString() {
                if (node == null) {
                    if (!string.IsNullOrEmpty(originalNodeName)) {
                        return $"(Destroyed) {originalNodeName}::{Core.instance.GetFieldDefinition(definitionId).name}";
                    }
                    return "(Uninitialized)";
                }

                return $"{node.name}::{Core.instance.GetFieldDefinition(definitionId).name}";
            }

            public void SetOverride(bool value) {
                node.SetOverride(definitionId, value);
            }
            public void SetOverride(string value) {
                node.SetOverride(definitionId, value);
            }
            public void SetOverrideRaw(int value) {
                node.SetOverrideRaw(definitionId, value);
            }
            public void ClearOverride() {
                node.ClearOverride(definitionId);
            }
        }
    }
}
