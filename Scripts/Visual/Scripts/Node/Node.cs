using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using OneHumus.Data;

namespace OneHamsa.Dexterity.Visual
{
    using Gate = NodeReference.Gate;

    [AddComponentMenu("Dexterity/Dexterity Node")]
    [DefaultExecutionOrder(Manager.nodeExecutionPriority)]
    public partial class Node : MonoBehaviour, IGateContainer
    {
        #region Static Functions
        // mainly for debugging graph problems
        private static Dictionary<BaseField, Node> fieldsToNodes = new Dictionary<BaseField, Node>();
        internal static Node ByField(BaseField f)
        {
            fieldsToNodes.TryGetValue(f, out var node);
            return node;
        }
        #endregion Static Functions

        #region Data Definitions
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
        #endregion Data Definitions

        #region Serialized Fields
        public List<NodeReference> referenceAssets = new List<NodeReference>();
        public StateFunctionGraph stateFunctionAsset;
        
        [State]
        public string initialState;

        [SerializeField]
        public List<Gate> customGates;

        [SerializeField]
        public List<OutputOverride> overrides;

        [State(allowEmpty: true)]
        public string overrideState;

        #endregion Serialized Fields

        #region Public Properties
        public NodeReference reference { get; private set; }

        // output fields of this node
        public ListMap<int, OutputField> outputFields { get; private set; } = new ListMap<int, OutputField>();
        public ListMap<int, OutputOverride> cachedOverrides { get; private set; } = new ListMap<int, OutputOverride>();
        
        public int activeState { get; private set; } = -1;
        public int overrideStateId { get; private set; } = -1;
        public double stateChangeTime { get; private set; }
        public virtual double currentTime => Time.unscaledTimeAsDouble;

        public event Action onEnabled;
        public event Action<Gate> onGateAdded;
        public event Action<Gate> onGateRemoved;
        public event Action onGatesUpdated;
        public event Action<int, int> onStateChanged;
        #endregion Public Properties

        #region Private Properties
        private List<BaseField> nonOutputFields = new List<BaseField>(10);
        int dirtyIncrement;
        int overridesIncrement;

        bool stateDirty;
        FieldsState fieldsState = new FieldsState(32);
        int[] stateFieldIds;
        double nextStateChangeTime;
        int pendingState = -1;

        public IEnumerable<Gate> allGates
        {
            get
            {
                if (reference != null)
                    foreach (var gate in reference.gates)
                        yield return gate;
            }
        }
        #endregion Private Properties

        #region Unity Events
        protected void OnEnable()
        {
            if (!EnsureValidState())
            {
                enabled = false;
                return;
            }

            Initialize();
            onEnabled?.Invoke();
        }

        protected void OnDisable()
        {
            Uninitialize();
        }

        protected void OnDestroy()
        {
            // only now it's ok to remove output fields
            foreach (var output in outputFields.Values.ToArray())
            {
                output.Finalize(this);
            }
            outputFields.Clear();
        }

        protected virtual void Update()
        {
            if (stateDirty)
            {
                // someone marked this dirty, check for new state
                var newState = GetState();
                if (newState == -1)
                {
                    Debug.LogWarning($"{name}: got -1 for new state, not updating");
                    return;
                }
                if (newState != pendingState)
                {
                    // add delay to change time
                    var delay = reference.GetDelay(activeState);
                    nextStateChangeTime = currentTime + delay?.delay ?? 0;
                    // don't trigger change if moving back to current state
                    pendingState = newState != activeState ? newState : -1;
                }
                stateDirty = false;
            }
            // change to next state (delay is accounted for already)
            if (nextStateChangeTime <= currentTime && pendingState != -1)
            {
                var oldState = activeState;

                activeState = pendingState;
                pendingState = -1;
                stateChangeTime = currentTime;

                onStateChanged?.Invoke(oldState, activeState);
            }
        }

        #endregion Unity Events

        #region General Methods
        private bool EnsureValidState()
        {
            if (referenceAssets.Count(a => a != null) == 0)
            {
                Debug.LogError("No references assigned", this);
                return false;
            }

            if (stateFunctionAsset == null)
            {
                Debug.LogError("No state function assigned", this);
                return false;
            }
            return true;
        }

        public void Initialize()
        {
            reference = ScriptableObject.CreateInstance<NodeReference>();
            reference.owner = this;
            reference.name = $"{name} (Live Reference)";
            reference.stateFunctionAsset = stateFunctionAsset;
            reference.extends.AddRange(referenceAssets);
            reference.Initialize(customGates);

            stateFieldIds = reference.stateFunction.GetFieldIDs().ToArray();

            // subscribe to more changes
            onGateAdded += RestartFields;
            onGateRemoved += RestartFields;
            onGatesUpdated += RestartFields;

            reference.onGateAdded += RestartFields;
            reference.onGateRemoved += RestartFields;
            reference.onGatesUpdated += RestartFields;

            RestartFields();
            CacheOverrides();
            CacheOverrideState();

            var defaultStateId = Manager.instance.GetStateID(initialState);
            if (defaultStateId == -1)
            {
                defaultStateId = reference.stateFunction.GetStateIDs().ElementAt(0);
                Debug.LogWarning($"no default state selected, selecting arbitrary", this);
            }

            activeState = defaultStateId;
        }

        public void Uninitialize()
        {
            // cleanup gates
            foreach (var gate in allGates.ToArray())
            {
                FinalizeGate(gate);
            }

            // unsubscribe
            onGateAdded -= RestartFields;
            onGateRemoved -= RestartFields;
            onGatesUpdated -= RestartFields;

            if (reference != null)
            {
                reference.onGateAdded -= RestartFields;
                reference.onGateRemoved -= RestartFields;
                reference.onGatesUpdated -= RestartFields;
            }
        }
        #endregion General Methods

        #region Fields & Gates
        void RestartFields(Gate g) => RestartFields();

        void RestartFields()
        {
            // unregister all fields. this might be triggered by editor, so go through this list
            //. in case original serialized data had changed (instead of calling FinalizeGate(gates))
            FinalizeFields(nonOutputFields.ToArray());
            // re-register all gates
            foreach (var gate in allGates.ToArray())  // might manipulate gates within the loop
                InitializeGate(gate);
        }

        void InitializeFields(int definitionId, IEnumerable<BaseField> fields)
        {
            // initialize all fields
            fields.ToList().ForEach(f =>
            {
                if (f == null || f is OutputField)  // OutputFields are self-initialized 
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
            fields.ToList().ForEach(f =>
            {
                if (f == null || f is OutputField)  // OutputFields are never removed
                    return;

                f.Finalize(this);
                Manager.instance?.UnregisterField(f);
                FinalizeFields(f.GetUpstreamFields());

                RemoveAudit(f);
            });
        }

        private void InitializeGate(Gate gate)
        {
            if (Application.isPlaying && !gate.Initialize())
                // invalid gate, don't add
                return;

            SetDirty();

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
        private void FinalizeGate(Gate gate)
        {
            SetDirty();

            FinalizeFields(new[] { gate.field });
        }

        /// <summary>
        /// Returns the node's output field. Slower than GetOutputField(int fieldId)
        /// </summary>
        /// <param name="name">Field name</param>
        /// <returns></returns>
        public OutputField GetOutputField(string name) 
            => GetOutputField(Manager.instance.GetFieldID(name));

        /// <summary>
        /// Returns the node's output field. Faster than GetOutputField(string name)
        /// </summary>
        /// <param name="fieldId">Field definition ID (from Manager)</param>
        /// <returns></returns>
        public OutputField GetOutputField(int fieldId)
        {
            // lazy initialization
            OutputField output;
            if (!outputFields.TryGetValue(fieldId, out output))
            {
                output = new OutputField();
                output.Initialize(this, fieldId);
                output.onValueChanged += MarkStateDirty;

                AuditField(output);
                stateDirty = true;
            }

            return output;
        }

        /// <summary>
        /// Sets the node as dirty. Forces output fields update
        /// </summary>
        public void SetDirty() => dirtyIncrement++;

        private void AuditField(BaseField field)
        {
            if (!(field is OutputField o))
                nonOutputFields.Add(field);
            else
                outputFields.Add(o.definitionId, o);

            fieldsToNodes[field] = this;
        }
        private void RemoveAudit(BaseField field)
        {
            if (!(field is OutputField o))
                nonOutputFields.Remove(field);
            else
            {
                Debug.LogWarning("OutputFields cannot be removed", this);
                return;
            }

            fieldsToNodes.Remove(field);
        }
        public void AddGate(Gate gate)
        {
            customGates.Add(gate);
            onGateAdded?.Invoke(gate);
        }

        public void RemoveGate(Gate gate)
        {
            customGates.Remove(gate);
            onGateRemoved?.Invoke(gate);
        }
        public void NotifyGatesUpdate()
        {
            onGatesUpdated?.Invoke();
        }

        public Gate GetGateAtIndex(int i)
        {
            return customGates[i];
        }
        #endregion Fields & Gates

        #region State Reduction
        private FieldsState FillFieldsState()
        {
            fieldsState.Clear();

            foreach (var fieldId in stateFieldIds)
            {
                var value = GetOutputField(fieldId).GetValue();
                // if this field isn't provided just assume default
                if (value == emptyFieldValue)
                {
                    value = defaultFieldValue;
                }
                fieldsState.Add((fieldId, value));
            }
            return fieldsState;
        }
        protected int GetState()
        {
            if (overrideStateId != -1)
                return overrideStateId;

            return reference.stateFunction.Evaluate(FillFieldsState());
        }

        private void MarkStateDirty(Node.OutputField field, int oldValue, int newValue) => stateDirty = true;
        #endregion State Reduction

        #region Overrides
        /// <summary>
        /// Sets a boolean override value
        /// </summary>
        /// <param name="fieldId">Field definition ID (from Manager)</param>
        /// <param name="value">Bool value for field</param>
        public void SetOverride(int fieldId, bool value)
        {
            var definition = Manager.instance.GetFieldDefinition(fieldId);
            if (definition.type != FieldType.Boolean)
                Debug.LogWarning($"setting a boolean override for a non-boolean field {definition.name}", this);

            SetOverrideRaw(fieldId, value ? 1 : 0);
        }

        /// <summary>
        /// Sets an enum override value
        /// </summary>
        /// <param name="fieldId">Field definition ID (from Manager)</param>
        /// <param name="value">Enum value for field (should appear in field definition)</param>
        public void SetOverride(int fieldId, string value)
        {
            var definition = Manager.instance.GetFieldDefinition(fieldId);
            if (definition.type != FieldType.Enum)
                Debug.LogWarning($"setting an enum (string) override for a non-enum field {definition.name}", this);

            int index;
            if ((index = Array.IndexOf(definition.enumValues, value)) == -1)
            {
                Debug.LogError($"trying to set enum {definition.name} value to {value}, " +
                    $"but it is not a valid enum value", this);
                return;
            }

            SetOverrideRaw(fieldId, index);
        }

        /// <summary>
        /// Sets raw override value
        /// </summary>
        /// <param name="fieldId">Field definition ID (from Manager)</param>
        /// <param name="value">Field value (0 or 1 for booleans, index for enums)</param>
        public void SetOverrideRaw(int fieldId, int value)
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

        /// <summary>
        /// Clears an existing override from a specific output field
        /// </summary>
        /// <param name="fieldId">Field definition ID (from Manager)</param>
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

        /// <summary>
        /// Overrides state to manual value
        /// </summary>
        /// <param name="fieldId">State ID (from Manager)</param>
        public void SetStateOverride(int state)
        {
            overrideStateId = state;
            stateDirty = true;

#if UNITY_EDITOR
            // in editor, write the overrideState string back
            overrideState = Manager.instance.GetStateAsString(state);
#else
            // in runtime, clear the string (to avoid re-caching)
            overrideState = null;
#endif
        }
        /// <summary>
        /// Overrides state to manual value
        /// </summary>
        /// <param name="fieldId">State ID (from Manager)</param>
        public void ClearStateOverride() => SetStateOverride(-1);

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

        private void CacheOverrideState()
        {
            if (!string.IsNullOrEmpty(overrideState))
                SetStateOverride(Manager.instance.GetStateID(overrideState));
        }

#if UNITY_EDITOR
        // update overrides every frame to allow setting overrides from editor
        void LateUpdate()
        {
            CacheOverrides();
        }
#endif
#endregion Overrides

#region Interface Implementation (Editor)
        StateFunctionGraph IGateContainer.stateFunctionAsset => stateFunctionAsset;
        Node IGateContainer.node => this;
#endregion Interface Implementation (Editor)
    }
}
