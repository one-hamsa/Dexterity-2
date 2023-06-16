using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    public abstract class DexterityBaseNode : MonoBehaviour, IHasStates
    {
        [Serializable]
        public class TransitionDelay
        {
            [State]
            public string beforeExitingState;
            public float waitFor = 0;
        }
        
        [State]
        public string initialState = StateFunction.kDefaultState;
        
        [SerializeField]
        public List<TransitionDelay> delays = new();

        [State(allowEmpty: true)]
        public string overrideState;

        #region Public Properties
        public double timeSinceStateChange;
        [NonSerialized]
        public double deltaTime;

        public event Action onEnabled;
        public event Action onDisabled;
        public event Action<int, int> onStateChanged;
        #endregion Public Properties

        #region Private Properties
        protected int activeState = StateFunction.emptyStateId;
        private int overrideStateId = StateFunction.emptyStateId;
        Dictionary<int, TransitionDelay> cachedDelays;

        protected bool stateDirty = true;
        double pendingStateChangeTime;
        int pendingState = -1;
        #endregion Private Properties

        #region Unity Events
        protected void OnEnable()
        {
            Initialize();

            // initialize can disable the node
            if (enabled)
                onEnabled?.Invoke();
        }

        protected void OnDisable()
        {
            Uninitialize();

            onDisabled?.Invoke();
        }

        protected virtual void OnDestroy()
        {
        }

        protected void Update() => UpdateInternal(ignoreDelays: false);

        protected virtual void UpdateInternal(bool ignoreDelays)
        {
            // doing this here gives the editor a chance to intervene
            deltaTime = Database.instance.deltaTime;
            
            if (stateDirty)
            {
                // someone marked this dirty, check for new state
                var newState = GetState();
                if (newState == -1)
                {
                    Debug.LogWarning($"{name}: got -1 for new state, not updating", this);
                    return;
                }
                if (newState != pendingState)
                {
                    // add delay to change time
                    pendingStateChangeTime = GetExitingStateDelay(activeState);
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
        protected virtual void Initialize()
        {
            // register my states 
            Database.instance.Register(this);
            
            // cache delays (from string to int)
            CacheDelays();
            // cache overrides to allow quick access internally
            CacheStateOverride();

            int initialStateId;
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
            
            activeState = overrideStateId != StateFunction.emptyStateId ? GetState() : initialStateId;

            // mark state as dirty - important if node was re-enabled
            stateDirty = true;
        }

        protected virtual void Uninitialize()
        {
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
        #endregion General Methods

        #region State Reduction
        protected virtual int GetState()
        {
            if (overrideStateId != -1)
                return overrideStateId;

            return StateFunction.emptyStateId;
        }
        public abstract HashSet<string> GetStateNames();
        public abstract HashSet<string> GetFieldNames();
        #endregion State Reduction

        #region Transitions
        private void CacheDelays()
        {
            cachedDelays = new Dictionary<int, TransitionDelay>();
            foreach (var delay in delays)
                cachedDelays[Database.instance.GetStateID(delay.beforeExitingState)] = delay;
        }

        private float GetExitingStateDelay(int state)
        {
            cachedDelays.TryGetValue(state, out var value);
            return value?.waitFor ?? 0f;
        }
        
        /// <summary>
        /// makes sure this node's state is updated and propagated to its modifiers (ignores node delays)
        /// </summary>
        public void UpdateState()
        {
            stateDirty = true;
            
            // make sure state is up-to-date
            UpdateInternal(ignoreDelays: true);

            foreach (var modifier in Modifier.GetModifiers(this))
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
            // make sure state is up-to-date
            UpdateInternal(ignoreDelays: true);

            foreach (var modifier in Modifier.GetModifiers(this))
            {
                // create a new transition state that is already pointing to active state
                modifier.InitializeTransitionState();
                // ignore delays
                modifier.JumpToState();
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

        protected virtual void OnValidate()
        {
            if (Application.isPlaying)
                return;
            
            var states = GetStateNames().ToList();
            if (states.Count > 0 && !states.Contains(initialState))
            {
                // only log if initialState is not empty
                if (!string.IsNullOrEmpty(initialState))
                    Debug.LogError($"Initial State {initialState} for node {name} is not part of node's states, " +
                                   $"selecting {states[0]} instead", this);
                
                initialState = states[0];
            }
        }
        
        #if UNITY_EDITOR
        public virtual void InitializeEditor() { }
        #endif
    }
}
