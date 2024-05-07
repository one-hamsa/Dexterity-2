using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Serialization;

namespace OneHamsa.Dexterity
{
    public abstract class BaseStateNode : MonoBehaviour, IHasStates
    {
        [Serializable]
        public class TransitionDelay
        {
            [State(allowEmpty: true)]
            public string beforeExitingState;
            [State(allowEmpty: true)]
            public string beforeEnteringState;
            public float waitFor = 0;
        }
        
        [State]
        public string initialState = StateFunction.kDefaultState;
        
        [SerializeField]
        public List<TransitionDelay> delays = new();
        [SerializeField, HideInInspector]
        private bool autoSyncModifiersStates = true;

        [State(allowEmpty: true)]
        public string overrideState;

        #region Public Properties
        [NonSerialized]
        public double timeSinceStateChange;
        [NonSerialized]
        public double deltaTime;
        [NonSerialized]
        public double timeScale = 1f;
        
        public int initialStateId { get; private set; } = -1;

        public event Action onEnabled;
        public event Action onDisabled;

        public event Action onParentTransformChanged;
        public event Action onChildTransformChanged;
        
        public event Action<int, int> onStateChanged;
        #endregion Public Properties

        #region Private Properties
        protected int activeState = StateFunction.emptyStateId;
        private int overrideStateId = StateFunction.emptyStateId;
        private Dictionary<(int enter, int exit), TransitionDelay> cachedDelays = new();

        protected bool stateDirty = true;
        double pendingStateChangeTime;
        int pendingState = -1;

        private BaseStateNode parentNode;

        internal HashSet<Modifier> nodeModifiers = new();

        [NonSerialized]
        private bool _performedFirstInitialization;
        
        public bool initialized { get; private set; }

        #endregion Private Properties

        #region Unity Events

        protected virtual void Awake()
        {
            nodeModifiers.EnsureCapacity(16);
        }

        protected virtual void OnDestroy()
        {
            Uninitialize(true);
        }

        protected void OnEnable()
        {
            Initialize();
            OnTransformParentChanged();

            // initialize can disable the node
            if (enabled)
                onEnabled?.Invoke();
        }

        private void UpdateParentNode()
        {
            var transformParent = transform.parent;
            if (transformParent != null)
                parentNode = transformParent.GetComponentInParent<BaseStateNode>();
        }

        protected void OnDisable()
        {
            Uninitialize(false);

            onDisabled?.Invoke();
        }

        private void OnTransformParentChanged()
        {
            var prevParentNode = parentNode;
            
            UpdateParentNode();
            
            if (parentNode != prevParentNode)
            {
                // Let all (previous) parents know their children have updated (this is done because OnTransformChildrenChanged()
                // is not recursive in the Unity implementation)
                var ancestorNode = prevParentNode;
                while (ancestorNode != null)
                {
                    ancestorNode.OnTransformChildrenChanged();
                    ancestorNode = ancestorNode.parentNode;
                }

                // Let all (new) parents know their children have updated (this is done because OnTransformChildrenChanged()
                // is not recursive in the Unity implementation)
                ancestorNode = parentNode;
                while (ancestorNode != null)
                {
                    ancestorNode.OnTransformChildrenChanged();
                    ancestorNode = ancestorNode.parentNode;
                }
            }
            
            onParentTransformChanged?.Invoke();
        }

        private void OnTransformChildrenChanged()
        {
            onChildTransformChanged?.Invoke();
        }

        protected void Update() => UpdateInternal(ignoreDelays: false);

        protected virtual void UpdateInternal(bool ignoreDelays)
        {
            // doing this here gives the editor a chance to intervene
            deltaTime = Database.instance.deltaTime * timeScale;
            
            if (stateDirty)
            {
                // someone marked this dirty, check for new state
                var newState = GetNextState();
                if (newState == -1)
                {
                    Debug.LogWarning($"{name}: got -1 for new state, not updating", this);
                    return;
                }
                if (newState != pendingState)
                {
                    // add delay to change time
                    pendingStateChangeTime = GetStateDelay(activeState, newState);
                    // don't trigger change if moving back to current state
                    pendingState = newState != activeState ? newState : -1;
                }
                
                stateDirty = false;
            }
            
            if (ignoreDelays)
                pendingStateChangeTime = 0f; 
            
            // change to next state (delay is accounted for already)
            if (pendingStateChangeTime <= 0 && pendingState != -1)
            {
                var oldState = activeState; 

                activeState = pendingState;
                pendingState = -1;
                timeSinceStateChange = 0;

                onStateChanged?.Invoke(oldState, activeState);
            }

            pendingStateChangeTime = Math.Max(0d, pendingStateChangeTime - deltaTime);
            timeSinceStateChange += deltaTime;
        }

        #endregion Unity Events

        #region General Methods
        public virtual bool ShouldAutoSyncModifiersStates() => autoSyncModifiersStates;
        public void SetAutoSyncModifiersStates(bool value) => autoSyncModifiersStates = value;

        public HashSet<Modifier> GetModifiers() => nodeModifiers;

        protected virtual void Initialize()
        {
            // register my states 
            Database.instance.Register(this);

            if (!_performedFirstInitialization)
            {
                // cache delays (from string to int)
                CacheDelays();
                // cache overrides to allow quick access internally
                CacheStateOverride();
            }

            if (!GetStateNames().Contains(initialState))
            {
                initialStateId = GetStateIDs().ElementAt(0);
                Debug.LogError($"Initial State {initialState} for node {name} is not part of node's states, " +
                               $"selecting arbitrary", this);
            }
            else
            {
                // find default state and define initial state
                initialStateId = Database.instance.GetStateID(initialState);
            }
            if (initialStateId == -1)
            {
                Debug.LogError($"internal error: initialState == -1 after initialization", this);
                enabled = false;
                return;
            }
            
            activeState = overrideStateId != StateFunction.emptyStateId ? GetNextState() : initialStateId;

            // mark state as dirty - important if node was re-enabled
            stateDirty = true;
            _performedFirstInitialization = true;
            initialized = true;
        }

        protected virtual void Uninitialize(bool duringTeardown)
        {
            initialized = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerable<int> GetStateIDs()
        {
            foreach (var stateName in GetStateNames())
                yield return Database.instance.GetStateID(stateName);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetActiveState() => activeState;
        public void SetActiveState_Editor(int newState) => activeState = newState;
        
        public bool IsStateDirty() => stateDirty;
        #endregion General Methods

        #region State Reduction

        public virtual int GetNextStateWithoutOverride() => StateFunction.emptyStateId;

        public int GetNextState()
        {
            if (overrideStateId != -1)
                return overrideStateId;

            return GetNextStateWithoutOverride();
        }
        public abstract HashSet<string> GetStateNames();
        public abstract HashSet<string> GetFieldNames();
        #endregion State Reduction

        #region Transitions
        private void CacheDelays()
        {
            cachedDelays.Clear();
            foreach (var delay in delays)
                cachedDelays[(
                    Database.instance.GetStateID(delay.beforeEnteringState),
                    Database.instance.GetStateID(delay.beforeExitingState)
                )] = delay;
        }

        private float GetStateDelay(int exitingState, int enteringState)
        {
            if (!cachedDelays.TryGetValue((enteringState, exitingState), out var value))
            {
                if (!cachedDelays.TryGetValue((enteringState, StateFunction.emptyStateId), out value))
                    cachedDelays.TryGetValue((StateFunction.emptyStateId, exitingState), out value);
            }
            return value?.waitFor ?? 0f;
        }
        
        /// <summary>
        /// procedurally sets a delay for a transition between two states
        /// </summary>
        /// <param name="exitingState"></param>
        /// <param name="enteringState"></param>
        /// <param name="delay"></param>
        public void SetStateDelay(int exitingState, int enteringState, float delay)
        {
            cachedDelays.Remove((enteringState, exitingState));
            cachedDelays[(enteringState, exitingState)] = new TransitionDelay
            {
                waitFor = delay
            };
        }
        
        /// <summary>
        /// makes sure this node's state is updated and propagated to its modifiers (ignores node delays)
        /// </summary>
        public void UpdateState(bool ignoreDelays = true)
        {
            stateDirty = true;
            if (!isActiveAndEnabled)
                return;
            
            // make sure state is up-to-date
            UpdateInternal(ignoreDelays: ignoreDelays);

            foreach (var modifier in GetModifiers())
            {
                // force updating now
                modifier.ForceTransitionUpdate();
            }
        }

        /// <summary>
        /// jumps to the end transition state for all modifiers, according to the current state (ignores node & modifier delays)
        /// </summary>
        public void JumpToState()
        {
            if (!isActiveAndEnabled)
                return;
            
            // make sure state is up-to-date
            UpdateInternal(ignoreDelays: true);

            foreach (var modifier in GetModifiers())
            {
                // ignore delays
                modifier.JumpToNodeState();
                // create a new transition state that is already pointing to active state
                modifier.InitializeTransitionState();
            }
            
            UpdateState();
        }

        /// <summary>
        /// jumps to the end transition state for all modifiers, according to the given state
        /// </summary>
        /// <param name="state"></param>
        public void JumpToState(int state)
        {
            if (state == StateFunction.emptyStateId)
            {
                Debug.LogError($"JumpToState: state == {StateFunction.emptyStateId}", this);
                return;
            }
            if (!GetStateIDs().Contains(state))
            {
                Debug.LogError($"JumpToState: state {state} is not part of node's states", this);
                return;
            }
            
            SetStateOverride(state);
            JumpToState();
            ClearStateOverride();
        }
        
        #endregion Transitions

        #region Overrides
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
            overrideState = Database.instance.GetStateAsString(state);
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

        private void CacheStateOverride()
        {
            if (!string.IsNullOrEmpty(overrideState))
                SetStateOverride(Database.instance.GetStateID(overrideState));
        }
        #endregion Overrides

        [ContextMenu("Force State Change")]
        public void ForceStateChange()
        {
            activeState = -1;
            UpdateState();
        }
        
        [ContextMenu("Toggle Auto Sync Modifiers States")]
        public void ToggleAutoSyncModifiersStates()
        {
            autoSyncModifiersStates = !autoSyncModifiersStates;
        }

        protected virtual void OnValidate()
        {
            if (Application.IsPlaying(this))
                return;
            
            var states = GetStateNames().ToList();
            if (states.Count > 0 && !states.Contains(initialState))
            {
                // only log if initialState is not empty
                if (!string.IsNullOrEmpty(initialState))
                    Debug.Log($"Initial State {initialState} for node {name} is not part of node's states, " +
                                   $"selecting {states[0]} instead", this);
                
                initialState = states[0];
            }
        }
        
        #if UNITY_EDITOR
        public virtual void InitializeEditor() { }
        #endif

        /// <summary>
        /// Allocates data structures, can be called before the node is enabled for faster initialization
        /// </summary>
        public virtual void Allocate()
        {
            Initialize();
            Uninitialize(false);
        }
    }
}
