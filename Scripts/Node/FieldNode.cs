using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace OneHamsa.Dexterity
{
    using Gate = NodeReference.Gate;

    [AddComponentMenu("Dexterity/Field Node")]
    [DefaultExecutionOrder(Manager.nodeExecutionPriority)]
    public partial class FieldNode : BaseStateNode, IGateContainer, IStepList
    {
        #region Static Functions
        // mainly for debugging graph problems
        private static Dictionary<BaseField, FieldNode> fieldsToNodes = new();
        internal static FieldNode ByField(BaseField f)
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
                if (fieldId == -1 && outputFieldDefinitionId != -1)
                    fieldId = outputFieldDefinitionId;
                
                if (fieldId != -1)
                {
                    outputFieldDefinitionId = fieldId;
                    outputFieldName = Database.instance.GetFieldDefinition(fieldId).GetName();
                    return true;
                }
                if (string.IsNullOrEmpty(outputFieldName))
                    return false;

                return (outputFieldDefinitionId = Database.instance.GetFieldID(outputFieldName)) != -1;
            }
        }
        #endregion Data Definitions

        #region Serialized Fields
        public List<NodeReference> referenceAssets = new();
     
        [SerializeField]
        public List<Gate> customGates = new();
        
        [SerializeField]
        public List<FieldDefinition> internalFieldDefinitions = new();

        [SerializeField]
        public List<OutputOverride> overrides = new();

        public List<StateFunction.Step> customSteps = new();

        #endregion Serialized Fields

        #region Public Properties

        // output fields of this node
        public SortedList<int, OutputField> outputFields = new();
        public SortedList<int, OutputOverride> cachedOverrides = new();

        public event Action<Gate> onGateAdded;
        public event Action<Gate> onGateRemoved;
        public event Action onGatesUpdated;
        #endregion Public Properties

        #region Private Properties
        private List<BaseField> nonOutputFields = new List<BaseField>(10);
        private int overridesDirtyIncrement;

        FieldMask fieldMask = new FieldMask(32);
        private int[] stateFieldIdsCache;
        private HashSet<string> fieldNames;
        private HashSet<string> stateNames;
        private StateFunction.StepEvaluationCache stepEvalCache;

        private bool _performedFirstInitialization;
        
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

        private static Stack<NodeReference> _processingStack = new(128);
        private static Stack<NodeReference> _orderedStack = new(128);
        
        protected override void Initialize()
        {
            // one more chance to run hotfix in case references changed but OnValidate() wasn't called
            FixSteps();

            if (!EnsureValidState())
            {
                enabled = false;
                return;
            }

            if (!_performedFirstInitialization)
            {
                // register all internal fields
                foreach (var field in internalFieldDefinitions)
                    Database.instance.RegisterInternalFieldDefinition(fieldDefinition: field);

                using var _ = HashSetPool<NodeReference>.Get(out var visitedNodeReferences);
                using var __ = ListPool<Gate>.Get(out var nodeGates);

                _orderedStack.Clear();
                _processingStack.Clear();
                for (int i = referenceAssets.Count-1; i >= 0; i--)
                    _processingStack.Push(referenceAssets[i]);

                while (_processingStack.Count > 0)
                {
                    var currentNodeRef = _processingStack.Pop();
                    if (visitedNodeReferences.Contains(currentNodeRef))
                        continue;
                    _orderedStack.Push(currentNodeRef);
                    visitedNodeReferences.Add(currentNodeRef);
                    for (int i = currentNodeRef.extends.Count-1; i >= 0; i--)
                    {
                        _processingStack.Push(currentNodeRef.extends[i]);
                    }
                }

                while (_orderedStack.Count > 0)
                {
                    // reverse the order [^1]
                    var reference = _orderedStack.Pop();

                    // register all internal fields
                    foreach (var field in reference.internalFieldDefinitions)
                        Database.instance.RegisterInternalFieldDefinition(fieldDefinition: field);

                    // register all functions
                    foreach (var stateFunc in reference.GetStateFunctionAssetsIncludingParents())
                        Database.instance.Register(stateFunc);

                    foreach (var gate in reference.gates)
                        nodeGates.Add(gate.CreateDeepClone());
                }

                // Add our own custom gates last
                customGates.InsertRange(0, nodeGates);
            }

            // run base initialize after registering states
            base.Initialize();

            if (!_performedFirstInitialization)
            {
                // then initialize step list
                (this as IStepList).InitializeSteps();

                // find all fields that are used by this node's state function
                stateFieldIdsCache = GetFieldIDs().ToArray();
            }

            _performedFirstInitialization = true;

            // subscribe to more changes
            onGateAdded += RestartFields;
            onGateRemoved += RestartFields;
            onGatesUpdated += RestartFields;

            // go through all the fields. initialize them, register them to manager and add them to internal structure
            RestartFields();
            // cache overrides to allow quick access internally
            CacheFieldOverrides();
            
            // last chance: if there are field overrides, reroute initial state
            if (cachedOverrides.Count > 0)
                activeState = GetState();
        }

        protected override void Uninitialize()
        {
            // cleanup gates
            foreach (var gate in customGates)
                FinalizeGate(gate);
            
            // unsubscribe
            onGateAdded -= RestartFields;
            onGateRemoved -= RestartFields;
            onGatesUpdated -= RestartFields;

            base.Uninitialize();
        }

        public IEnumerable<int> GetFieldIDs()
        {
            foreach (var name in GetFieldNames())
            {
                yield return Database.instance.GetFieldID(name);
            }
        }
        
        public override HashSet<string> GetStateNames() {
            if (!Application.isPlaying || stateNames == null)
            {
                stateNames = new HashSet<string>();
                
                foreach (var name in (this as IStepList).GetStepListStateNames())
                    stateNames.Add(name);
            }
            
            return stateNames;
        }
        public override HashSet<string> GetFieldNames() {
            if (!Application.isPlaying || fieldNames == null)
            {
                fieldNames = new HashSet<string>();

                foreach (var name in (this as IStepList).GetStepListFieldNames())
                    fieldNames.Add(name);
            }
            
            return fieldNames;
        }
        
        public IEnumerable<FieldDefinition> GetInternalFieldDefinitions()
        {
            foreach (var reference in referenceAssets)
            {
                if (reference == null)
                    continue;

                foreach (var fieldDefinition in reference.GetInternalFieldDefinitions())
                    yield return fieldDefinition;
            }
            foreach (var fieldDefinition in internalFieldDefinitions)
            {
                var f = fieldDefinition;
                f.isInternal = true;
                yield return f;
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
            foreach (var gate in customGates)  // might manipulate gates within the loop
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
                if (f is null or OutputField)  // OutputFields are self-initialized 
                    return;

                f.Initialize(this, definitionId);
                
                Manager.instance.RegisterField(f);
                
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

            // make sure output field for gate is initialized
            GetOutputField(gate.outputFieldDefinitionId);

            try
            {
                InitializeFields(gate.outputFieldDefinitionId, new[] { gate.field });
            }
            catch (BaseField.FieldInitializationException e)
            {
                Debug.LogException(e, this);
                Debug.LogWarning($"caught FieldInitializationException, removing {gate} from {name}.{gate.outputFieldName}", this);
                FinalizeGate(gate);
            }

            SetDirty();
        }
        private void FinalizeGate(Gate gate)
        {
            FinalizeFields(new[] { gate.field });
            SetDirty();
        }

        /// <summary>
        /// Returns the node's output field. Slower than GetOutputField(int fieldId)
        /// </summary>
        /// <param name="name">Field name</param>
        /// <returns></returns>
        public OutputField GetOutputField(string name) 
            => GetOutputField(Database.instance.GetFieldID(name));

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

        public event Action onDirty;

        /// <summary>
        /// Sets the node as dirty. Forces gates update
        /// </summary>
        public void SetDirty()
        {
            onDirty?.Invoke();
        }

        private void AuditField(BaseField field)
        {
            if (!(field is OutputField o))
            {
                nonOutputFields.Add(field);
                fieldsToNodes[field] = this;
            }
            else
                outputFields.Add(o.definitionId, o);
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
            RestartFields();
            onGateAdded?.Invoke(gate);
        }

        public void RemoveGate(Gate gate)
        {
            customGates.Remove(gate);
            RestartFields();
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

            foreach (var fieldId in stateFieldIdsCache)
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

        private void MarkStateDirty(FieldNode.OutputField field, int oldValue, int newValue) => stateDirty = true;
        #endregion State Reduction

        #region Overrides
        /// <summary>
        /// Sets a boolean override value
        /// </summary>
        /// <param name="fieldId">Field definition ID (from Manager)</param>
        /// <param name="value">Bool value for field</param>
        public void SetOverride(int fieldId, bool value)
        {
            var definition = Database.instance.GetFieldDefinition(fieldId);
            if (definition.type != FieldType.Boolean)
                Debug.LogWarning($"setting a boolean override for a non-boolean field {definition.GetName()}", this);

            SetOverrideRaw(fieldId, value ? 1 : 0);
        }

        /// <summary>
        /// Sets an enum override value
        /// </summary>
        /// <param name="fieldId">Field definition ID (from Manager)</param>
        /// <param name="value">Enum value for field (should appear in field definition)</param>
        public void SetOverride(int fieldId, string value)
        {
            var definition = Database.instance.GetFieldDefinition(fieldId);
            if (definition.type != FieldType.Enum)
                Debug.LogWarning($"setting an enum (string) override for a non-enum field {definition.GetName()}", this);

            int index;
            if ((index = Array.IndexOf(definition.enumValues, value)) == -1)
            {
                Debug.LogError($"trying to set enum {definition.GetName()} value to {value}, " +
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
            if (cachedOverrides.TryGetValue(fieldId, out var @override))
            {
                overrides.Remove(@override);
                CacheFieldOverrides();
            }
            else
            {
                // don't warn - this allows to call it to verify the override does not exist.
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
        IEnumerable<FieldDefinition> IGateContainer.GetInternalFieldDefinitions() 
            => GetInternalFieldDefinitions();

        IEnumerable<string> IGateContainer.GetWhitelistedFieldNames()
            => GetFieldNames();

        FieldNode IGateContainer.node => this;

        public int GetLastEvaluationResult() => GetActiveState();
        List<StateFunction.Step> IStepList.steps => customSteps;
        #endregion Interface Implementation (Editor)
        
        #region Misc (Editor)
#if UNITY_EDITOR
        public override void InitializeEditor()
        {
            ((IStepList)this).InitializeSteps();
        }
#endif
        #endregion
    }
}
