using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    using Gate = NodeReference.Gate;

    [AddComponentMenu("Dexterity/Dexterity Node")]
    [DefaultExecutionOrder(Manager.nodeExecutionPriority)]
    public partial class Node : DexterityBaseNode, IGateContainer, IStepList
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
                    outputFieldName = Core.instance.GetFieldDefinition(fieldId).name;
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
     
        [SerializeField]
        public List<Gate> customGates = new List<Gate>();

        [SerializeField]
        public List<OutputOverride> overrides = new List<OutputOverride>();

        public List<StateFunction.Step> customSteps = new List<StateFunction.Step>();

        #endregion Serialized Fields

        #region Public Properties
        public NodeReference reference { get; private set; }

        // output fields of this node
        public Dictionary<int, OutputField> outputFields = new Dictionary<int, OutputField>();
        public Dictionary<int, OutputOverride> cachedOverrides = new Dictionary<int, OutputOverride>();

        public event Action<Gate> onGateAdded;
        public event Action<Gate> onGateRemoved;
        public event Action onGatesUpdated;
        #endregion Public Properties

        #region Private Properties
        private List<BaseField> nonOutputFields = new List<BaseField>(10);
        int gatesDirtyIncrement;
        int overridesDirtyIncrement;

        FieldMask fieldMask = new FieldMask(32);
        int[] stateFieldIds;
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
        protected override void OnDestroy()
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
            base.OnDestroy();
        }
        #endregion Unity Events

        #region General Methods
        private bool EnsureValidState()
        {
            if (customSteps.Count == 0)
            {
                Debug.LogError("No steps assigned to node", this);
                return false;
            }
            return true;
        }

        protected override void Initialize()
        {
            // one more chance to run hotfix in case references changed but OnValidate() wasn't called
            FixSteps();

            if (!EnsureValidState())
            {
                enabled = false;
                return;
            }

            // only needed once
            if (reference == null) {
                // create a runtime instance of this scriptable object to allow changing it
                reference = ScriptableObject.CreateInstance<NodeReference>();
                // define this node as its owner
                reference.owner = this;
                reference.name = $"{name} (Live Reference)";
                // copy all references from this node to the runtime instance
                reference.extends.AddRange(referenceAssets);
                // initialize reference (this will create the runtime version with all the inherited gates)
                reference.Initialize(customGates);
            }

            // run base initialize after registering states
            base.Initialize();
            // then initialize step list
            (this as IStepList).InitializeSteps();

            // find all fields that are used by this node's state function
            stateFieldIds = GetFieldIDs().ToArray();

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
            // cache overrides to allow quick access internally
            CacheFieldOverrides();
        }

        protected override void Uninitialize()
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

            base.Uninitialize();
        }

        private IEnumerable<int> GetFieldIDs()
        {
            foreach (var name in GetFieldNames())
            {
                yield return Core.instance.GetFieldID(name);
            }
        }
        private IEnumerable<int> GetStateIDs()
        {
            foreach (var stateName in GetStateNames())
                yield return Core.instance.GetStateID(stateName);
        }
        public override IEnumerable<string> GetStateNames() {
            namesSet.Clear();
            
            foreach (var name in (this as IStepList).GetStepListStateNames()) {
                if (namesSet.Add(name)) {
                    yield return name;
                }
            }
        }
        public override IEnumerable<string> GetFieldNames() {
            namesSet.Clear();

            foreach (var name in (this as IStepList).GetStepListFieldNames()) {
                if (namesSet.Add(name)) {
                    yield return name;
                }
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
        protected override int GetState()
        {
            var baseState = base.GetState();
            if (baseState != StateFunction.emptyStateId)
                return baseState;

            if (stepEvalCache == null)
                stepEvalCache = (this as IStepList).BuildStepCache();

            return IStepList.Evaluate(stepEvalCache, GenerateFieldMask());
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
            GetOutputField(fieldId).CacheValue();
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
        protected override void OnValidate()
        {
            base.OnValidate();
            // this will cause editor to crash if selecting multiple nodes, so we call it from CustomEditor instead
            // FixSteps();

            if (Application.isPlaying)
                CacheFieldOverrides();
        }

        void Reset() {
            customSteps.Clear();
            FixSteps();
        }

        
        public void FixSteps()
        {
            // fix duplicate IDs
            var ids = new HashSet<int>();
            for (int i = 0; i < customSteps.Count; i++)
            {
                var step = customSteps[i];
                if (ids.Contains(step.id))
                {
                    Debug.LogWarning($"duplicate step ID {step.id} in {name}");
                    step.id = GetNextStepID();
                    #if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(this);
                    #endif
                }
                ids.Add(step.id);
            }
            
            AddReferencesToStateFunctions();
            AddFallbackStateIfNeeded();
        }

        private void AddReferencesToStateFunctions()
        {
            // find all references in steps
            var refs = new HashSet<StateFunction>();
            foreach (var step in customSteps)
            {
                if (step.type == StateFunction.Step.Type.Reference && step.reference_stateFunction != null)
                    refs.Add(step.reference_stateFunction);
            }

            // add all state functions from references
            var additionIndex = 0;
            foreach (var reference in referenceAssets)
            {
                if (reference == null)
                    continue;

                foreach (var asset in reference.GetStateFunctionAssetsIncludingParents())
                {
                    if (refs.Add(asset))
                    {
                        customSteps.Insert(additionIndex++, new StateFunction.Step
                        {
                            id = GetNextStepID(),
                            type = StateFunction.Step.Type.Reference,
                            reference_stateFunction = asset
                        });
                        #if UNITY_EDITOR
                            UnityEditor.EditorUtility.SetDirty(this);
                        #endif
                    }
                }
            }
        }

        private void AddFallbackStateIfNeeded()
        {
            if ((this as IStepList).HasFallback())
                return;

            customSteps.Add(new StateFunction.Step
            {
                id = GetNextStepID(),
                type = StateFunction.Step.Type.Result,
                result_stateName = StateFunction.kDefaultState,
            });
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        private int GetNextStepID()
        {
            return customSteps.Count == 0 ? 0 : customSteps.Max(s => s.id) + 1;
        }
        #endregion Overrides

        #region Interface Implementation (Editor)
        IEnumerable<string> IGateContainer.GetStateNames() => GetStateNames();
        IEnumerable<string> IGateContainer.GetFieldNames() => GetFieldNames();

        Node IGateContainer.node => this;

        public int lastEvaluationResult => activeState;
        List<StateFunction.Step> IStepList.steps => customSteps;
        #endregion Interface Implementation (Editor)
    }
}
