using System;
using System.Collections.Generic;
using System.Data;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    using Gate = NodeReference.Gate;

    public partial class FieldNode
    {

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
            public FieldNode node;
            public string originalNodeName { get; private set; }
            protected int valueWithoutOverride = emptyFieldValue;
            // saves a list of all gates of this output field
            protected List<Gate> cachedGates = new List<Gate>();

            #region Optimization Fields
            // saves a reference to an override to avoid unnecessary lookup
            OutputOverride cachedOverride = null;
            // tracks the last overrides dirty increment from the node, if it's different we need to update
            int overridesDirtyIncrement = -1;
            #endregion
            
            // cache this to avoid native pointer inequality
            private bool finalized;

            protected OutputOverride fieldOverride
            {
                get {
                    // perform pointer null check without invoking Unity's Equals
                    if (Equals(node, null)) 
                    {
                        cachedOverride = null;
                    } else {
                        if (overridesDirtyIncrement == node.overridesDirtyIncrement)
                            return cachedOverride;
                        
                        if (!node.cachedOverrides.TryGetValue(definitionId, out cachedOverride))
                            cachedOverride = null;
                        
                        overridesDirtyIncrement = node.overridesDirtyIncrement;
                    }
                    
                    return cachedOverride;
                }
            }

            public override BaseField CreateDeepClone()
            {
                throw new DataException("OutputFields shouldn't get DeepCloned!");
            }

            protected override void Initialize(FieldNode context)
            {
                base.Initialize(context);

                // save reference to node
                node = context;
                originalNodeName = node.name;
                
                node.onEnabled += OnNodeEnabled;
                node.onDisabled += OnNodeDisabled;
            }

            public override void Finalize(FieldNode context)
            {
                base.Finalize(context);

                SetValue(defaultFieldValue);
                
                if (node != null)
                {
                    node.onEnabled -= OnNodeEnabled;
                    node.onDisabled -= OnNodeDisabled;
                }
                
                node = null;
                
                finalized = true;
            }

            private void OnNodeEnabled()
            {
                // if the node is enabled, we might need to update the value
                RefreshUpstreams();
            }
            
            private void OnNodeDisabled()
            {
                // if the node is disabled, we might need to reset the value
                RefreshUpstreams();
            }

            /// <summary>
            /// Refreshes the list of relevant gates and recalculates the value of the field.
            /// </summary>
            public void RefreshUpstreams()
            {
                cachedGates.Clear();
                ClearUpstreamFields();

                if (finalized || !node.isActiveAndEnabled)
                    return;
                
                // cache gates
                foreach (var gate in node.customGates)
                {
                    if (gate.outputFieldDefinitionId != definitionId || gate.field == null)
                        continue;

                    cachedGates.Add(gate);
                    AddUpstreamField(gate.field);
                }
            }

            protected override void OnUpstreamsChanged(List<BaseField> upstreams = null)
            {
                base.OnUpstreamsChanged(upstreams);
                if (finalized || !node.isActiveAndEnabled)
                {
                    // if the node is disabled, it's always 0
                    valueWithoutOverride = 0;
                }
                else
                {
                    // merge it with the other gate according to the field's type
                    if (definition.type == FieldType.Boolean)
                    {
                        var result = false;
                        foreach (var gate in cachedGates)
                        {
                            if (!gate.field.initialized)
                                continue;
                            
                            try
                            {
                                var found = false;

                                if ((gate.overrideType & Gate.OverrideType.Additive) != 0)
                                {
                                    result |= gate.field.GetBooleanValue();
                                    found = true;
                                }

                                if ((gate.overrideType & Gate.OverrideType.Subtractive) != 0)
                                {
                                    result &= gate.field.GetBooleanValue();
                                    found = true;
                                }

                                if (!found)
                                    Debug.LogError(
                                        $"Unknown override type {gate.overrideType} for field {gate.field.definition.GetName()}",
                                        node);
                            }
                            catch (Exception e)
                            {
                                Debug.LogException(e, node);
                            }
                        }

                        valueWithoutOverride = result ? 1 : 0;
                    }
                    else if (definition.type == FieldType.Enum)
                    {
                        if (cachedGates.Count > 0)
                            // override: take last one
                            valueWithoutOverride = cachedGates[^1].field.value;
                        else
                            // default enum value is first value
                            valueWithoutOverride = 0;
                    }
                }

                // if there's an override, just use it
                var outputOverride = fieldOverride;
                if (outputOverride != null && !finalized && node.isActiveAndEnabled) 
                {
                    SetValue(outputOverride.value, upstreams);
                }
                else
                {
                    SetValue(valueWithoutOverride, upstreams);
                }
            }

            // for debug purposes only, mostly for editor view (to avoid masking the original field value)
            public int GetValueWithoutOverride()
            {
                return valueWithoutOverride;
            }
            
            public override string ToShortString() {
                if (finalized) {
                    if (!string.IsNullOrEmpty(originalNodeName)) {
                        return $"(Destroyed) {originalNodeName}::{Database.instance.GetFieldDefinition(definitionId).GetName()}";
                    }
                    return "(Uninitialized)";
                }

                return $"{node.name}::{Database.instance.GetFieldDefinition(definitionId).GetName()}";
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
