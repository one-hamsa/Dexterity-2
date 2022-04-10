using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    using Gate = NodeReference.Gate;

    [AddComponentMenu("Dexterity/Dexterity Node")]
    [DefaultExecutionOrder(Manager.nodeExecutionPriority)]
    public partial class Node : MonoBehaviour, IGateContainer, IStatesProvider, IStepList
    {
        [Serializable]
        public class TransitionDelay
        {
            [State]
            public string beforeExitingState;
            public float waitFor = 0;
        }

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
            [HideInInspector]
            public string name;

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

                return (outputFieldDefinitionId = Core.instance.GetFieldID(outputFieldName)) != -1;
            }
        }
        #endregion Data Definitions

        #region Serialized Fields
        public List<NodeReference> referenceAssets = new List<NodeReference>();
        public List<StateFunction> stateFunctionAssets = new List<StateFunction>();
        
        [State]
        public string initialState = StateFunction.kDefaultState;

        [SerializeField]
        public List<Gate> customGates = new List<Gate>();

        [SerializeField]
        public List<TransitionDelay> delays = new List<TransitionDelay>();

        [SerializeField]
        public List<OutputOverride> overrides = new List<OutputOverride>();

        [State(allowEmpty: true)]
        public string overrideState;

        public List<StateFunction.Step> customSteps = new List<StateFunction.Step>();

        #endregion Serialized Fields

        #region Public Properties
        public NodeReference reference { get; private set; }

        // output fields of this node
        public Dictionary<int, OutputField> outputFields = new Dictionary<int, OutputField>();
        public Dictionary<int, OutputOverride> cachedOverrides = new Dictionary<int, OutputOverride>();
        
        // don't change this directly, use fields
        [NonSerialized]
        public int activeState = -1;
        // don't change this directly, use SetStateOverride
        [NonSerialized]
        public int overrideStateId = -1;
        // don't change this directly
        [NonSerialized]
        public double stateChangeTime;
        // don't change this directly
        [NonSerialized]
        public double currentTime;

        public event Action onEnabled;
        public event Action<Gate> onGateAdded;
        public event Action<Gate> onGateRemoved;
        public event Action onGatesUpdated;
        public event Action<int, int> onStateChanged;
        #endregion Public Properties

        #region Private Properties
        private List<BaseField> nonOutputFields = new List<BaseField>(10);
        int gatesDirtyIncrement;
        int overridesDirtyIncrement;
        Dictionary<int, TransitionDelay> cachedDelays;

        bool stateDirty = true;
        FieldMask fieldMask = new FieldMask(32);
        int[] stateFieldIds;
        double nextStateChangeTime;
        int pendingState = -1;
        private HashSet<string> namesSet = new HashSet<string>();
        private StateFunction.StepEvaluationCache stepEvalCache;

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

            if (reference != null) {
                Destroy(reference);
                reference = null;
            }
        }

        protected virtual void Update()
        {
            currentTime = 
                #if UNITY_2020_1_OR_NEWER
                Time.unscaledTimeAsDouble
            #else
                Time.unscaledTime
            #endif
            ;

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
                    var delay = GetExitingStateDelay(activeState);
                    nextStateChangeTime = currentTime + delay?.waitFor ?? 0;
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
            if (stateFunctionAssets.Count == 0 && referenceAssets.Count == 0)
            {
                Debug.LogError("No state functions or references assigned to node", this);
                return false;
            }
            return true;
        }

        public void Initialize()
        {
            // only needed once
            if (reference == null) {
                // create a runtime instance of this scriptable object to allow changing it
                reference = ScriptableObject.CreateInstance<NodeReference>();
                // define this node as its owner
                reference.owner = this;
                reference.name = $"{name} (Live Reference)";
                // copy all references from this node to the runtime instance
                reference.stateFunctionAssets.AddRange(stateFunctionAssets);
                reference.extends.AddRange(referenceAssets);
                // initialize reference (this will create the runtime version with all the inherited gates)
                reference.Initialize(customGates);
                // register my states, too
                Core.instance.RegisterStates(this);
            }

            // find all fields that are used by this node's state function
            stateFieldIds = reference.GetFieldIDs().ToArray();

            // subscribe to more changes
            onGateAdded += RestartFields;
            onGateRemoved += RestartFields;
            onGatesUpdated += RestartFields;

            // same for reference (gates can be modified either on the node or on its reference)
            reference.onGateAdded += RestartFields;
            reference.onGateRemoved += RestartFields;
            reference.onGatesUpdated += RestartFields;

            // go through all the fields. initialize them, register them to manager and add them to internal structure
            RestartFields();
            // cache delays (from string to int)
            CacheDelays();
            // cache overrides to allow quick access internally
            CacheFieldOverrides();
            CacheStateOverride();

            // find default state and define initial state
            var initialStateId = Core.instance.GetStateID(initialState);
            if (initialStateId == -1)
            {
                initialStateId = reference.GetStateIDs().ElementAt(0);
                Debug.LogWarning($"no initial state selected, selecting arbitrary", this);
            }
            activeState = initialStateId;

            // mark state as dirty - important if node was re-enabled
            stateDirty = true;
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
            // cache all outputs
            foreach (var output in outputFields.Values) {
                output.RefreshReferences();
                output.CacheValue();
            }
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
                if (Manager.instance != null)
                    Manager.instance.UnregisterField(f);
                FinalizeFields(f.GetUpstreamFields());

                RemoveAudit(f);
            });
        }

        private void InitializeGate(Gate gate)
        {
            if (Application.isPlaying && !gate.Initialize()) {
                // invalid gate, don't add
                Debug.LogWarning($"{name}: {gate} is invalid, not adding", this);
                return;
            }

            SetDirty();

            // make sure output field for gate is initialized
            GetOutputField(gate.outputFieldDefinitionId);

            try
            {
                InitializeFields(gate.outputFieldDefinitionId, new[] { gate.field });
            }
            catch (BaseField.FieldInitializationException)
            {
                Debug.LogWarning($"caught FieldInitializationException, removing {gate} from {name}.{gate.outputFieldName}", this);
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
            => GetOutputField(Core.instance.GetFieldID(name));

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
        /// Builds all node's fields' cache
        /// </summary>
        public void RebuildCache() {
            foreach (var field in nonOutputFields.Concat(outputFields.Values)) {
                field.RebuildCache();
                field.RefreshReferences();
                field.CacheValue();
            }
        }

        /// <summary>
        /// Sets the node as dirty. Forces gates update
        /// </summary>
        public void SetDirty() => gatesDirtyIncrement++;

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
        void IGateContainer.AddGate(Gate gate)
        {
            customGates.Add(gate);
            onGateAdded?.Invoke(gate);
        }

        void IGateContainer.RemoveGate(Gate gate)
        {
            customGates.Remove(gate);
            onGateRemoved?.Invoke(gate);
        }
        public void NotifyGatesUpdate()
        {
            onGatesUpdated?.Invoke();
        }

        int IGateContainer.GetGateCount() => customGates.Count;
        Gate IGateContainer.GetGateAtIndex(int i)
        {
            return customGates[i];
        }
        #endregion Fields & Gates

        #region State Reduction
        private FieldMask GenerateFieldMask()
        {
            fieldMask.Clear();

            foreach (var fieldId in stateFieldIds)
            {
                var value = GetOutputField(fieldId).GetValue();
                // if this field isn't provided just assume default
                if (value == emptyFieldValue)
                {
                    value = defaultFieldValue;
                }
                fieldMask.Add((fieldId, value));
            }
            return fieldMask;
        }
        protected int GetState()
        {
            if (overrideStateId != -1)
                return overrideStateId;

            var mask = GenerateFieldMask();
            var stateId = reference.Evaluate(mask);
            if (stateId != StateFunction.emptyStateId)
                return stateId;

            if (stepEvalCache == null)
                stepEvalCache = this.BuildStepCache();

            return this.Evaluate(stepEvalCache, mask);
        }

        private void MarkStateDirty(Node.OutputField field, int oldValue, int newValue) => stateDirty = true;
        #endregion State Reduction

        #region Transitions
        private void CacheDelays()
        {
            cachedDelays = new Dictionary<int, TransitionDelay>();
            foreach (var delay in delays)
                cachedDelays[Core.instance.GetStateID(delay.beforeExitingState)] = delay;
        }

        private TransitionDelay GetExitingStateDelay(int state)
        {
            cachedDelays.TryGetValue(state, out var value);
            return value;
        }

        #endregion Transitions

        #region Overrides
        /// <summary>
        /// Sets a boolean override value
        /// </summary>
        /// <param name="fieldId">Field definition ID (from Manager)</param>
        /// <param name="value">Bool value for field</param>
        public void SetOverride(int fieldId, bool value)
        {
            var definition = Core.instance.GetFieldDefinition(fieldId);
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
            var definition = Core.instance.GetFieldDefinition(fieldId);
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
                CacheFieldOverrides();
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
                CacheFieldOverrides();
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
            // in editor, write to the overrideState string (this can be called in edit time)
            overrideState = Core.instance.GetStateAsString(state);
#else
            // in runtime, clear the string
            overrideState = null;
#endif
        }
        /// <summary>
        /// Overrides state to manual value
        /// </summary>
        /// <param name="fieldId">State ID (from Manager)</param>
        public void ClearStateOverride() => SetStateOverride(-1);

        void CacheFieldOverrides()
        {
            cachedOverrides.Clear();
            foreach (var o in overrides)
            {
                o.Initialize();
                cachedOverrides[o.outputFieldDefinitionId] = o;
            }
            overridesDirtyIncrement++;
        }

        private void CacheStateOverride()
        {
            if (!string.IsNullOrEmpty(overrideState))
                SetStateOverride(Core.instance.GetStateID(overrideState));
        }

        // update overrides when selected to allow setting overrides from editor
        void OnValidate()
        {
            // add all state functions from references
            foreach (var reference in referenceAssets) {
                if (reference == null)
                    continue;

                foreach (var asset in reference.GetStateFunctionAssetsIncludingParents()) {
                    if (!stateFunctionAssets.Contains(asset)) {
                        stateFunctionAssets.Add(asset);
                    }
                }
            }

            AddFallbackStateIfNeeded();

            if (Application.isPlaying)
                CacheFieldOverrides();
        }

        private void AddFallbackStateIfNeeded()
        {
            foreach (var sf in stateFunctionAssets) {
                if (sf == null)
                    continue;
                if (sf.HasFallback())
                    return;
            }

            if (this.HasFallback())
                return;

            customSteps.Add(new StateFunction.Step {
                type = StateFunction.Step.Type.Result,
                result_stateName = StateFunction.kDefaultState,
            });
            #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }
        #endregion Overrides

        #region Interface Implementation (Editor)
        HashSet<StateFunction> stateFunctionsSet = new HashSet<StateFunction>();
        public IEnumerable<StateFunction> GetStateFunctionAssetsIncludingReferences() {
            stateFunctionsSet.Clear();
            foreach (var asset in stateFunctionAssets) {
                if (asset == null)
                    continue;

                if (stateFunctionsSet.Add(asset)) {
                    yield return asset;
                }
            }
            foreach (var reference in referenceAssets) {
                if (reference == null)
                    continue;

                foreach (var asset in reference.GetStateFunctionAssetsIncludingParents()) {
                    if (stateFunctionsSet.Add(asset)) {
                        yield return asset;
                    }
                }
            }
        }

        IEnumerable<string> IStatesProvider.GetStateNames() {
            namesSet.Clear();
            foreach (var name in StateFunction.EnumerateStateNames(GetStateFunctionAssetsIncludingReferences())) {
                if (namesSet.Add(name)) {
                    yield return name;
                }
            }

            foreach (var name in this.GetStates()) {
                if (namesSet.Add(name)) {
                    yield return name;
                }
            }
        }

        IEnumerable<string> IStatesProvider.GetFieldNames() {
            namesSet.Clear();
            foreach (var name in StateFunction.EnumerateFieldNames(GetStateFunctionAssetsIncludingReferences())) {
                if (namesSet.Add(name)) {
                    yield return name;
                }
            }

            foreach (var name in this.GetFieldNames()) {
                if (namesSet.Add(name)) {
                    yield return name;
                }
            }
        }

        IEnumerable<string> IGateContainer.GetStateNames() => (this as IStatesProvider).GetStateNames();
        IEnumerable<string> IGateContainer.GetFieldNames() => (this as IStatesProvider).GetFieldNames();

        Node IGateContainer.node => this;

        List<StateFunction.Step> IStepList.steps => customSteps;
        #endregion Interface Implementation (Editor)
    }
}
