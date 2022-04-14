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
        public List<TransitionDelay> delays = new List<TransitionDelay>();

        [State(allowEmpty: true)]
        public string overrideState;

        #region Public Properties
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
        public event Action<int, int> onStateChanged;
        #endregion Public Properties

        #region Private Properties
        Dictionary<int, TransitionDelay> cachedDelays;

        protected bool stateDirty = true;
        double nextStateChangeTime;
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
        protected virtual void Initialize()
        {
            // cache delays (from string to int)
            CacheDelays();
            // cache overrides to allow quick access internally
            CacheStateOverride();

            // find default state and define initial state
            var initialStateId = Core.instance.GetStateID(initialState);
            if (initialStateId == -1)
            {
                initialStateId = GetStateIDs().ElementAt(0);
                Debug.LogWarning($"no initial state selected, selecting arbitrary", this);
            }
            activeState = initialStateId;

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
        protected abstract int GetState();
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

        private TransitionDelay GetExitingStateDelay(int state)
        {
            cachedDelays.TryGetValue(state, out var value);
            return value;
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
    }
}
