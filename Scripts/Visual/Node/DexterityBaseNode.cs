using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
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
        // don't change this directly, use fields
        [NonSerialized]
        public int activeState = StateFunction.emptyStateId;
        // don't change this directly, use SetStateOverride
        [NonSerialized]
        public int overrideStateId = StateFunction.emptyStateId;
        // don't change this directly
        [NonSerialized]
        public double timeSinceStateChange;

        public double deltaTime;

        public event Action onEnabled;
        public event Action<int, int> onStateChanged;
        #endregion Public Properties

        #region Private Properties
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
        }

        protected virtual void OnDestroy()
        {
        }

        protected virtual void Update()
        {
            // doing this here gives the editor a chance to intervene
            deltaTime = Core.instance.deltaTime;
            
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
            Core.instance.Register(this);
            
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
                initialStateId = Core.instance.GetStateID(initialState);
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

        private IEnumerable<int> GetStateIDs()
        {
            foreach (var stateName in GetStateNames())
                yield return Core.instance.GetStateID(stateName);
        }
        #endregion General Methods

        #region State Reduction
        protected virtual int GetState()
        {
            if (overrideStateId != -1)
                return overrideStateId;

            return StateFunction.emptyStateId;
        }
        public abstract IEnumerable<string> GetStateNames();
        public abstract IEnumerable<string> GetFieldNames();
        #endregion State Reduction

        #region Transitions
        private void CacheDelays()
        {
            cachedDelays = new Dictionary<int, TransitionDelay>();
            foreach (var delay in delays)
                cachedDelays[Core.instance.GetStateID(delay.beforeExitingState)] = delay;
        }

        private float GetExitingStateDelay(int state)
        {
            cachedDelays.TryGetValue(state, out var value);
            return value?.waitFor ?? 0f;
        }
        
        /// <summary>
        /// makes sure this node's state is updated and propagated to its modifiers
        /// </summary>
        public void UpdateState()
        {
            // make sure state is up-to-date
            Update();

            foreach (var modifier in Modifier.GetModifiers(this))
            {
                // force updating now
                modifier.ForceTransitionUpdate();
                // and actually call update to avoid one frame lag
                modifier.Update();
            }
        }

        /// <summary>
        /// jumps to the end transition state for all modifiers, according to the current state
        /// </summary>
        public void JumpToState()
        {
            // make sure state is up-to-date
            Update();

            foreach (var modifier in Modifier.GetModifiers(this))
            {
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

        private void CacheStateOverride()
        {
            if (!string.IsNullOrEmpty(overrideState))
                SetStateOverride(Core.instance.GetStateID(overrideState));
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
    }
}
