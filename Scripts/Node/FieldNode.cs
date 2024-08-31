using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Profiling;

namespace OneHamsa.Dexterity
{
    using Gate = NodeReference.Gate;

    [AddComponentMenu("Dexterity/Field Node")]
    [DefaultExecutionOrder(Manager.nodeExecutionPriority)]
    public partial class FieldNode : BaseStateNode, IGateContainer, IStepList
    {
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
        public InsertSortList<OutputField> outputFields = new();
        public InsertSortList<OutputOverride> cachedOverrides = new();

        public event Action<Gate> onGateAdded;
        public event Action<Gate> onGateRemoved;
        public event Action onGatesUpdated;
        
        #endregion Public Properties

        #region Private Properties
        private List<BaseField> nonOutputFields = new List<BaseField>(10);
        private int overridesDirtyIncrement;
        
        private List<int> stateFieldIdsCache;
        private HashSet<string> fieldNames;
        private HashSet<string> stateNames;
        private List<(StateFunction.Step step, int depth)> stepEvalCache;

        [NonSerialized]
        private bool _performedFirstInitialization_FieldNode;

        #endregion Private Properties

        #region Unity Events
        protected override void OnDestroy()
        {
            // only now it's ok to remove output fields
            foreach (var pair in outputFields.keyValuePairs)
            {
                pair.Value.onValueChanged -= MarkStateDirty;
                pair.Value.Uninitialize(this);
            }

            outputFields.Clear();
            
            if (stateFieldIdsCache != null)
                ListPool<int>.Release(stateFieldIdsCache);
            
            if (fieldNames != null)
                HashSetPool<string>.Release(fieldNames);

            if (stateNames != null)
                HashSetPool<string>.Release(stateNames);
            
            if (stepEvalCache != null)
                ListPool<(StateFunction.Step step, int depth)>.Release(stepEvalCache);

            stateFieldIdsCache = null;
            fieldNames = null;
            stateNames = null;
            stepEvalCache = null;

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
            if (!_performedFirstInitialization_FieldNode)
                FixSteps();

            Profiler.BeginSample("FieldNode Initialize: EnsureValidState");
            if (!EnsureValidState())
            {
                enabled = false;
                Profiler.EndSample();
                return;
            }
            Profiler.EndSample();

            if (!_performedFirstInitialization_FieldNode)
            {
                Profiler.BeginSample("FieldNode Initialize: Register internal fields");
                // register all internal fields
                foreach (var field in internalFieldDefinitions)
                    Database.instance.RegisterInternalFieldDefinition(fieldDefinition: field);
                Profiler.EndSample();

                Profiler.BeginSample("FieldNode Initialize: Build processing stack");
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
                Profiler.EndSample();

                Profiler.BeginSample("FieldNode Initialize: register stack");
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
                Profiler.EndSample();
            }

            // run base initialize after registering states
            base.Initialize();

            if (!_performedFirstInitialization_FieldNode)
            {
                Profiler.BeginSample("FieldNode Initialize: Initialize steps");
                // then initialize step list
                (this as IStepList).InitializeSteps();

                // find all fields that are used by this node's state function
                stateFieldIdsCache = ListPool<int>.Get();
                stateFieldIdsCache.AddRange(GetFieldIDs());
                Profiler.EndSample();

                // go through all the fields. initialize them, register them to manager and add them to internal structure
                Profiler.BeginSample("FieldNode Initialize: Restart Fields");
                RestartFields();
                Profiler.EndSample();

                // cache overrides to allow quick access internally
                Profiler.BeginSample("FieldNode Initialize: Cache Field Overrides");
                CacheFieldOverrides();
                Profiler.EndSample();
                
                // To trigger caching of step list
                Profiler.BeginSample("FieldNode Initialize: GetNextState (caching of step list)");
                activeState = GetNextState();
                
                _performedFirstInitialization_FieldNode = true;
                Profiler.EndSample();
            }
            else
            {
                Profiler.BeginSample("FieldNode Initialize: GetNextState (caching of step list)");
                // last chance: if there are field overrides, reroute initial state
                if (cachedOverrides.Count > 0)
                    activeState = GetNextState();
                Profiler.EndSample();
            }
        }

        protected override void Uninitialize()
        {
            foreach (var gate in customGates)
                UninitializeGate(gate);
            
            base.Uninitialize();
        }

        public IEnumerable<int> GetFieldIDs()
        {
            foreach (var name in GetFieldNames())
            {
                yield return Database.instance.GetFieldID(name);
            }
        }
        
        public override HashSet<string> GetStateNames()
        {
            HashSet<string> myStateNames;
            if (!Application.IsPlaying(this))
                myStateNames = new HashSet<string>();
            else
            {
                if (stateNames == null)
                    stateNames = HashSetPool<string>.Get();

                myStateNames = stateNames;
            }

            foreach (var name in (this as IStepList).GetStepListStateNames())
                myStateNames.Add(name);
            
            return myStateNames;
        }
        public override HashSet<string> GetFieldNames()
        {
            HashSet<string> myFieldNames;
            if (!Application.IsPlaying(this))
                myFieldNames = new HashSet<string>();
            else
            {
                if (fieldNames == null)
                    fieldNames = HashSetPool<string>.Get();

                myFieldNames = fieldNames;
            }

            foreach (var name in (this as IStepList).GetStepListFieldNames())
                myFieldNames.Add(name);
            
            return myFieldNames;
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
            //. in case original serialized data had changed (instead of calling UninitializeGate(gates))
            using (var _ = ListPool<BaseField>.Get(out var list))
            {
                list.AddRange(nonOutputFields);
                foreach (var field in list)
                    UninitializeField(field);
            }

            // re-register all gates
            foreach (var gate in customGates)  // might manipulate gates within the loop
                InitializeGate(gate);
            
            // cache all outputs
            foreach (var pair in outputFields.keyValuePairs)
            {
                var output = pair.Value;
                output.RefreshUpstreams();
            }
        }

        void InitializeField(int definitionId, BaseField field)
        {
            if (field is null or OutputField)  // OutputFields are self-initialized 
                return;

            field.Initialize(this, definitionId);
                
            foreach (var upstreamField in field.GetUpstreamFields())
                InitializeField(definitionId, upstreamField);

            AuditField(field);
        }

        void UninitializeField(BaseField field)
        {
            if (field is null or OutputField or { initialized: false })  // OutputFields are never removed
                return;

            field.Uninitialize(this);
            
            var upstreams = field.GetUpstreamFields();
            if (upstreams != null)
            {
                foreach (var upstreamField in field.GetUpstreamFields())
                    UninitializeField(upstreamField);
            }

            RemoveAudit(field);
        }

        private void InitializeGate(Gate gate)
        {
            if (Application.IsPlaying(this) && !gate.Initialize()) {
                // invalid gate, don't add
                Debug.LogWarning($"{name}: {gate} is invalid, not adding", this);
                return;
            }

            // make sure output field for gate is initialized
            GetOutputField(gate.outputFieldDefinitionId);

            try
            {
                InitializeField(gate.outputFieldDefinitionId, gate.field);
            }
            catch (BaseField.FieldInitializationException e)
            {
                Debug.LogException(e, this);
                Debug.LogWarning($"caught FieldInitializationException, removing {gate} from {name}.{gate.outputFieldName}", this);
                UninitializeGate(gate);
            }

            SetDirty();
        }
        private void UninitializeGate(Gate gate)
        {
            UninitializeField(gate.field);
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
            }
            else
                outputFields.AddOrUpdate(o.definitionId, o);
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
        }

        public void AddGate(Gate gate)
        {
            customGates.Add(gate);
            if (Application.IsPlaying(this))
                RestartFields();
            onGateAdded?.Invoke(gate);
        }

        public void RemoveGate(Gate gate)
        {
            customGates.Remove(gate);
            if (Application.IsPlaying(this))
                RestartFields();
            onGateRemoved?.Invoke(gate);
        }
        public void NotifyGatesUpdate()
        {
            if (Application.IsPlaying(this))
                RestartFields();
            onGatesUpdated?.Invoke();
        }

        int IGateContainer.GetGateCount() => customGates.Count;
        Gate IGateContainer.GetGateAtIndex(int i)
        {
            return customGates[i];
        }
        #endregion Fields & Gates

        #region State Reduction
        private void GenerateFieldMask(List<(int field, int value)> fieldMask)
        {
            fieldMask.Clear();

            foreach (var fieldId in stateFieldIdsCache)
            {
                var value = GetOutputField(fieldId).value;
                // if this field isn't provided just assume default
                if (value == BaseField.emptyFieldValue)
                {
                    value = BaseField.defaultFieldValue;
                }
                fieldMask.Add((fieldId, value));
            }
        }

        public override int GetNextStateWithoutOverride()
        {
            var baseState = base.GetNextStateWithoutOverride();
            if (baseState != StateFunction.emptyStateId)
                return baseState;

            List<(StateFunction.Step step, int depth)> usedStepCahce;
            if (Application.IsPlaying(this))
            {
                if (stepEvalCache == null)
                    stepEvalCache = ListPool<(StateFunction.Step step, int depth)>.Get();

                usedStepCahce = stepEvalCache;
            }
            else
            {
                usedStepCahce = new();
            }
            
            (this as IStepList).PopulateStepCache(usedStepCahce);

            using (ListPool<(int field, int value)>.Get(out var fieldMask))
            {
                GenerateFieldMask(fieldMask);
                return IStepList.Evaluate(usedStepCahce, fieldMask);
            }
        }

        private void MarkStateDirty(BaseField.ValueChangeEvent e) => stateDirty = true;
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
            bool isNew = false;
            if (!cachedOverrides.Contains(fieldId))
            {
                var newOverride = new OutputOverride();
                newOverride.Initialize(fieldId);

                overrides.Add(newOverride);
                CacheFieldOverrides();
                isNew = true;
            }
            var overrideOutput = cachedOverrides[fieldId];
            if (overrideOutput.value == value && !isNew)
                return;
            
            overrideOutput.value = value;
            GetOutputField(fieldId).RefreshUpstreams();
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
                GetOutputField(fieldId).RefreshUpstreams();
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

            if (Application.IsPlaying(this) && Database.instance != null)
                CacheFieldOverrides();
        }

        void Reset() {
            customSteps.Clear();
            FixSteps();
        }

        
        public void FixSteps()
        {
            Profiler.BeginSample("Fix Steps");

            // fix duplicate IDs
            using var _ = HashSetPool<int>.Get(out var ids);
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
            Profiler.EndSample();
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

        HashSet<string> IGateContainer.GetWhitelistedFieldNames()
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

        public override void Allocate()
        {
            base.Allocate();
            // To trigger caching of step list
            _ = GetNextState();
        }
    }
}
