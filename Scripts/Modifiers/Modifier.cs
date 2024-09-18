using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using OneHamsa.Dexterity.Utilities;
using UnityEditor;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    [DefaultExecutionOrder(Manager.modifierExecutionPriority)]
    [ModifierPropertyDefinition("Property")]
    public abstract class Modifier : TransitionBehaviour, IReferencesNode
    {
        public enum DelayDirection
        {
            Enter,
            Exit
        }
        [Serializable]
        public class TransitionDelay
        {
            public DelayDirection direction;
            [State]
            public string state;
            public float waitFor = 0;
        }

        [SerializeField]
        public BaseStateNode _node;

        [SerializeReference]
        public List<PropertyBase> properties = new();
        
        [SerializeField]
        public List<TransitionDelay> delays = new();

        [HideInInspector] public List<string> lastSeenStates = new();
        [HideInInspector] public bool manualStateEditing = false;

        private int[] cachedStates;
        private Dictionary<(DelayDirection, int), TransitionDelay> cachedDelays = new();
        private int renderedState = StateFunction.emptyStateId;

        protected override int[] states => cachedStates;
        protected override double deltaTime => myDeltaTime;
        protected override double timeSinceStateChange => myTimeSinceStateChange;
        private double myDeltaTime = 0d;
        private double myTimeSinceStateChange = 0d;
        private int activeState;

        private bool _initialized;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BaseStateNode GetNode() => TryFindNode();
        public float transitionProgress => GetTransitionProgress(GetNode().GetActiveState());
        public virtual bool animatableInEditor => isActiveAndEnabled;

        protected Transform _transform;

        public float GetTransitionProgress(int state)
        {
            if (transitionState == null)
                return default;
            transitionState.TryGetValue(state, out var result);
            return result;
        }

        public virtual void PrepareTransition_Editor(string initialState, string targetState)
        {
            _initialized = false;
            Awake();
            CacheDelays();
            
            cachedStates = properties.Select(p => Database.instance.GetStateID(p.state)).ToArray();
            var initialStateId = Database.instance.GetStateID(initialState);
            if (Array.IndexOf(cachedStates, initialStateId) == -1)
                cachedStates = cachedStates.Append(initialStateId).ToArray();
            var targetStateId = Database.instance.GetStateID(targetState);
            if (Array.IndexOf(cachedStates, targetStateId) == -1)
                cachedStates = cachedStates.Append(targetStateId).ToArray();
            
            activeState = initialStateId;
            InitializeTransitionState();
            myTimeSinceStateChange = 0d;
            
            renderedState = activeState;
            activeState = targetStateId;
        }

        public void ProgressTime_Editor(double dt)
        {
            myDeltaTime = dt;
            myTimeSinceStateChange += dt;
        }

        private Dictionary<int, PropertyBase> propertiesCache = null;
        private bool foundNode;
        
        [ContextMenu("Toggle Manual State Editing")]
        public void ToggleManualStateEditing()
        {
            manualStateEditing = !manualStateEditing;
        }

        public override bool IsChanged()
        {
            return base.IsChanged() 
                   || IsTransitionDelayed() 
                   || (Application.IsPlaying(this) && GetNodeActiveStateWithoutDelay() != activeState);
        }

        public PropertyBase GetProperty(int stateId)
        {
            // runtime
            if (propertiesCache != null)
            {
                if (!propertiesCache.ContainsKey(stateId))
                {
                    // try to return initial state
                    if (propertiesCache.TryGetValue(TryFindNode().initialStateId, out var property))
                    {
                        return property;
                    }
                    
                    if (!manualStateEditing)
                    {
                        Debug.LogWarning($"property for state = {Database.instance.GetStateAsString(stateId)} not found on Modifier [{GetType().Name}] {name}" +
                                         $" (probably states were added to node {GetNode().name} without updating modifier)," +
                                         $" and no initial state was found", this);
                    }
                    
                    // just return first
                    foreach (var p in propertiesCache.Values)
                    {
                        return p;
                    }
                }

                return propertiesCache[stateId];
            }

            // editor
            foreach (var prop in properties)
            {
                if (Database.instance.GetStateID(prop.state) == stateId)
                {
                    return prop;
                }
                
            }

            // just return something
            return properties[0];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PropertyBase GetActiveProperty() => GetProperty(GetNode().GetActiveState());

        protected virtual void HandleNodeStateChange(int oldState, int newState)
        {
            // subscribe to refreshes (removed when transition ends)
            if (isActiveAndEnabled && Manager.isAlive)
                Manager.instance.SubscribeToUpdates(this);
            
            HandleStateChange(renderedState, GetNodeActiveStateWithDelay());
        }
        
        /// <summary>
        /// called when the modifier state changes, after possible delays
        /// </summary>
        /// <param name="oldState"></param>
        /// <param name="newState"></param>
        public virtual void HandleStateChange(int oldState, int newState)
        {
            renderedState = newState;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool IsTransitionDelayed() => timeSinceStateChange < GetStateDelay(renderedState, GetNode().GetActiveState());
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetActiveState() => activeState;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected int GetNodeActiveStateWithDelay() 
            => IsTransitionDelayed() ? renderedState : GetNodeActiveStateWithoutDelay();
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected int GetNodeActiveStateWithoutDelay() => GetNode().GetActiveState();

        [Serializable]
        public abstract class PropertyBase
        {
            public string state;
            public string savedPropertyKey;
            public string localStateReference;
            
            public virtual PropertyBase Clone()
            {
                var clone = (PropertyBase) MemberwiseClone();
                return clone;
            }
        }

        protected virtual void InitializeCacheData()
        {
            if (_initialized)
                return;
            
            var node = GetNode();
            if (!enabled || node == null || !node.enabled || !node.initialized)
                return;
            
            CacheDelays();
            cachedStates ??= node.GetStateIDs().ToArray();
            
            propertiesCache = new Dictionary<int, PropertyBase>();
            foreach (var prop in properties)
            {
                if (prop == null) 
                {
                    Debug.LogError("Null property in Modifier", this);
                    continue;
                }
                var id = Database.instance.GetStateID(prop.state);
                if (id == -1)
                {
                    // those properties are kept serialized in order to maintain history, no biggie
                    continue;
                }

                if (Array.IndexOf(cachedStates, id) == -1)
                {
                    // stale property, ignore
                    continue;
                }

                if (propertiesCache.ContainsKey(id))
                {
                    Debug.LogError($"Duplicate property for state {prop.state} in {GetType().Name}", this);
                    continue;
                }

                var chosen = prop;
                if (!string.IsNullOrEmpty(prop.savedPropertyKey))
                {
                    var saved = Database.instance.settings.GetSavedProperty(prop.GetType(), prop.savedPropertyKey);
                    if (saved == null)
                        Debug.LogError($"Saved property {prop.savedPropertyKey} not found in Modifier {name}, using old value", this);
                    else
                        chosen = saved;
                }
                else if (!string.IsNullOrEmpty(prop.localStateReference))
                {
                    var internalReference = properties.FirstOrDefault(p => p.state == prop.localStateReference);
                    if (internalReference == null)
                        Debug.LogError($"Internal state reference {prop.localStateReference} not found in Modifier {name}, using old value", this);
                    else
                        chosen = internalReference;
                }

                propertiesCache.Add(id, chosen);
            }
            if (propertiesCache.Count == 0)
            {
                Debug.LogError($"No properties found for modifier {name} ({GetType().Name})", this);
                return;
            }

            _initialized = true;
        }

        protected override void Awake()
        {
            _transform = transform;
            InitializeCacheData();
        }

        private void HandleNodeEnabled() => HandleNodeEnabled(false);
        private void HandleNodeEnabled(bool duringSelfOnEnable)
        {
            // maybe it didn't happen because node was disabled
            InitializeCacheData();
            
            activeState = GetNode().GetActiveState();
            try
            {
                HandleNodeStateChange(activeState, activeState);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                enabled = false;
                return;
            }

            InitializeTransitionState();

            // When this is called during OnEnalbe of my modifier, TransitionBehavior.OnEnable will force refresh anyway (optimization)
            if (!duringSelfOnEnable)
                Refresh();
        }
        
        private void CacheDelays()
        {
            cachedDelays.Clear();
            foreach (var delay in delays)
                cachedDelays[(delay.direction, Database.instance.GetStateID(delay.state))] = delay;
        }

        public void AddDelay(TransitionDelay delay)
        {
            delays.Add(delay);
            CacheDelays();
        }
        
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float GetStateDelay(int exitingState, int enteringState)
        {
            cachedDelays.TryGetValue((DelayDirection.Enter, enteringState), out var enterDelay);
            cachedDelays.TryGetValue((DelayDirection.Exit, exitingState), out var exitDelay);
            return Mathf.Max(enterDelay?.waitFor ?? 0f, exitDelay?.waitFor ?? 0f);
        }
        
        protected override void OnEnable()
        {
            TryFindNode();
            InitializeCacheData();
            if (!EnsureValidState())
            {
                if (Application.IsPlaying(this))
                    enabled = false;
                return;
            }

            Manager.instance.SubscribeToUpdates(this);
            _node.nodeModifiers.Add(this);

            if (_node.isActiveAndEnabled)
                HandleNodeEnabled(true);
            
            // always treat node enabled to be able to handle state changes
            _node.onEnabled -= HandleNodeEnabled;
            _node.onEnabled += HandleNodeEnabled;

            _node.onStateChanged -= HandleNodeStateChange;
            _node.onStateChanged += HandleNodeStateChange;

            base.OnEnable();
        }
        protected override void OnDisable()
        {
            base.OnDisable();
            
            if (Manager.isAlive)
                Manager.instance.UnsubscribeFromUpdates(this);
            
            if (_node != null) {
                _node.onEnabled -= HandleNodeEnabled;
                _node.onStateChanged -= HandleNodeStateChange;
                _node.nodeModifiers.Remove(this);
            }
            
            foundNode = false;
        }

        public override void Refresh()
        {
            if (Application.IsPlaying(this))
            {
                // wait for node to be enabled
                var node = GetNode();
                if (!node.isActiveAndEnabled)
                {
                    transitionChanged = false;
                    return;
                }

                myTimeSinceStateChange = node.timeSinceStateChange;
                myDeltaTime = node.deltaTime;
                activeState = GetNodeActiveStateWithDelay();
            }

            base.Refresh();
            
            if (!IsChanged() && Manager.isAlive)
                // unsubscribe from refreshes until state changes
                Manager.instance.UnsubscribeFromUpdates(this);
            
            if (renderedState != activeState)
                HandleStateChange(renderedState, activeState);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected BaseStateNode TryFindNode()
        {
            if (Application.IsPlaying(this) && foundNode) 
                return _node;
            
            BaseStateNode current = _node;
            Transform parent = transform;
            while (current == null && parent != null)
            {
                // include inactive if we're inactive
                if (!gameObject.activeInHierarchy || parent.gameObject.activeInHierarchy)
                    current = parent.GetComponent<BaseStateNode>();

                parent = parent.parent;
            }

            if (current != null && Application.IsPlaying(this))
            {
                foundNode = true;
                _node = current;
            }

            return current;
        }

        protected bool EnsureValidState()
        {
            if (_node == null)
            {
                Debug.LogError($"Node not found for modifier {name} ({GetType().Name})", this);
                return false;
            }

            if (properties.Count == 0 || propertiesCache == null || propertiesCache.Count == 0)
            {
                #if UNITY_EDITOR
                Debug.Log($"No properties found for modifier {name} ({GetType().Name})", this);
                #endif
                return false;
            }

            return true;
        }

        /// <summary>
        /// jumps to current state (skips delays)
        /// </summary>
        public void JumpToNodeState()
        {
            renderedState = activeState = _node.GetActiveState();
        }

        protected virtual void Reset() {
            if (this is ISupportValueFreeze supportValueFreeze) {
                supportValueFreeze.FreezeValue();
            }
        }
        
        /// <summary>
        /// Allocates data structures, can be called before the modifier is enabled for faster initialization
        /// </summary>
        public virtual void Allocate()
        {
            InitializeCacheData();
        }

        #if UNITY_EDITOR
        public virtual (string, LogType) GetEditorComment() => default;
        #endif
        
        
        public bool SyncStates()
        {
            if (manualStateEditing)
            {
                if (properties.FindIndex(p => p.state == StateFunction.kDefaultState) == 0)
                    return false;

                RemoveState(StateFunction.kDefaultState);
                var newProp = AddState(StateFunction.kDefaultState);
                // move to be the first (because first is the fallback in case of missing state)
                properties.Remove(newProp);
                properties.Insert(0, newProp);
                
                return true;
            }

            // don't sync if can't find any node
            if (GetNode() == null)
                return false;
            
            var states = ((IHasStates)this).GetStateNames().ToHashSet();
            var targetStates = properties.Select(p => p?.state).ToList();
            var changed = false;
            foreach (var state in states)
            {
                if (!targetStates.Contains(state))
                {
                    AddState(state);
                    changed = true;
                }
            }

            for (var i = 0; i < targetStates.Count; i++)
            {
                var state = targetStates[i];
                if (state == null
                    || !states.Contains(state)
                    || targetStates.IndexOf(state) != i)
                {
                    RemoveState(state);
                    changed = true;
                }
            }

            return changed;
        }

        public PropertyBase AddState(string state)
        {
            // get property info, iterate through parent classes to support inheritance
            Type propType = null;
            var objType = GetType();
            while (propType == null)
            {
                var attr = objType.GetCustomAttribute<ModifierPropertyDefinitionAttribute>(true);
                propType = attr?.propertyType
                    ?? (!string.IsNullOrEmpty(attr.propertyName) ? objType.GetNestedType(attr.propertyName) : null);
                    
                objType = objType.BaseType;
            }

            var newProp = (Modifier.PropertyBase)Activator.CreateInstance(propType);
            newProp.state = state;
            properties.Add(newProp);
            if (this is ISupportPropertyFreeze supportPropertyFreeze) 
                supportPropertyFreeze.FreezeProperty(newProp);
            
            #if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            #endif
            
            return newProp;
        }

        public void RemoveState(string state)
        {
            properties.RemoveAll(p => p?.state == state);
            #if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            #endif
        }

        private void OnValidate()
        {
            if (!Application.IsPlaying(this))
                SyncStates();
        }
    }
}