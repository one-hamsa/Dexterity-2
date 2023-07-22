using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using OneHamsa.Dexterity.Utilities;
using UnityEngine;
using UnityEngine.Serialization;

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
        
        private static Dictionary<BaseStateNode, HashSet<Modifier>> nodesToModifiers = new();
        public static IEnumerable<Modifier> GetModifiers(BaseStateNode node)
        {
            if (nodesToModifiers.TryGetValue(node, out var modifiers))
                return modifiers;
            return System.Linq.Enumerable.Empty<Modifier>();
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
        private int lastState = StateFunction.emptyStateId;

        protected override int[] states => cachedStates;
        protected override double deltaTime => myDeltaTime;
        protected override double timeSinceStateChange => myTimeSinceStateChange;
        private double myDeltaTime = 0d;
        private double myTimeSinceStateChange = 0d;
        private int activeState;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BaseStateNode GetNode() => TryFindNode();
        public float transitionProgress => GetTransitionProgress(GetNode().GetActiveState());
        public virtual bool animatableInEditor => isActiveAndEnabled;

        public float GetTransitionProgress(int state)
        {
            if (transitionState == null)
                return default;
            transitionState.TryGetValue(state, out var result);
            return result;
        }

        public virtual void PrepareTransition_Editor(string initialState, string targetState)
        {
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
            myTimeSinceStateChange = 1d;
            
            lastState = activeState;
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
            return base.IsChanged() || IsTransitionDelayed();
        }

        public PropertyBase GetProperty(int stateId)
        {
            // runtime
            if (propertiesCache != null)
            {
                if (!propertiesCache.ContainsKey(stateId))
                {
                    if (!manualStateEditing)
                        Debug.LogWarning($"property for state = {Database.instance.GetStateAsString(stateId)} not found on Modifier {name}" +
                                         $" (probably states were added to node {GetNode().name} without updating modifier)", this);
                    // just return first
                    foreach (var p in propertiesCache.Values)
                        return p;
                }
                return propertiesCache[stateId];
            }

            // editor
            foreach (var prop in properties)
                if (Database.instance.GetStateID(prop.state) == stateId)
                    return prop;

            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PropertyBase GetActiveProperty() => GetProperty(GetNode().GetActiveState());

        public virtual void HandleStateChange(int oldState, int newState)
        {
            // subscribe to refreshes (removed when transition ends)
            if (Manager.instance != null)
                Manager.instance.AddModifier(this);
            lastState = oldState;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool IsTransitionDelayed() => timeSinceStateChange < GetStateDelay(lastState, GetNode().GetActiveState());
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetActiveState() => activeState;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetNodeActiveStateWithDelay() 
            => IsTransitionDelayed() ? lastState : GetNode().GetActiveState();

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

        protected override void Awake()
        {
            propertiesCache = new Dictionary<int, PropertyBase>();
            foreach (var prop in properties)
            {
                if (prop == null) {
                    Debug.LogError("Null property in Modifier", this);
                    continue;
                }
                var id = Database.instance.GetStateID(prop.state);
                if (id == -1)
                {
                    // those properties are kept serialized in order to maintain history, no biggie
                    continue;
                }
                if (propertiesCache.ContainsKey(id))
                {
                    Debug.LogError($"Duplicate property for state {prop.state} in Modifier", this);
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
        }

        private void HandleNodeEnabled()
        {
            if (!enabled)
                return;
            
            activeState = GetNode().GetActiveState();
            HandleStateChange(activeState, activeState);

            cachedStates = GetNode().GetStateIDs().ToArray();

            CacheDelays();

            InitializeTransitionState();
            Refresh();
        }
        
        private void CacheDelays()
        {
            cachedDelays = new();
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
            if (!EnsureValidState())
            {
                if (Application.isPlaying)
                    enabled = false;
                return;
            }
            
            Manager.instance.AddModifier(this);
            if (!nodesToModifiers.TryGetValue(_node, out var modifiers))
            {
                modifiers = nodesToModifiers[_node] = new HashSet<Modifier>();
            }
            modifiers.Add(this);

            if (_node.isActiveAndEnabled)
                HandleNodeEnabled();
            else
            {
                _node.onEnabled -= HandleNodeEnabled;
                _node.onEnabled += HandleNodeEnabled;
            }

            _node.onStateChanged -= HandleStateChange;
            _node.onStateChanged += HandleStateChange;

            base.OnEnable();
        }
        protected override void OnDisable()
        {
            base.OnDisable();
            
            if (Manager.instance != null)
                Manager.instance.RemoveModifier(this);
            
            if (_node != null) {
                _node.onEnabled -= HandleNodeEnabled;
                _node.onStateChanged -= HandleStateChange;
                if (nodesToModifiers.TryGetValue(_node, out var modifiers))
                    modifiers.Remove(this);
            }
            
            foundNode = false;
        }

        public override void Refresh()
        {
            if (Application.isPlaying)
            {
                // wait for node to be enabled
                var node = GetNode();
                if (!node.isActiveAndEnabled)
                    return;

                myTimeSinceStateChange = node.timeSinceStateChange;
                myDeltaTime = node.deltaTime;
                activeState = GetNodeActiveStateWithDelay();
            }

            base.Refresh();
            
            if (!IsChanged() && Manager.instance != null)
                // unsubscribe from refreshes until state changes
                Manager.instance.RemoveModifier(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected BaseStateNode TryFindNode()
        {
            if (Application.isPlaying && foundNode) 
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

            if (current != null && Application.isPlaying)
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

            if (!_node.enabled)
            {
                // XXX here comes garbage: unity might set a node to disabled when its gameObject is destroyed
                //. before it is == null, but also call OnEnable on child components. so here we are, not printing
                //. this warning because unity. 

                try
                {
                    Debug.LogWarning($"Node {_node.gameObject.GetPath()} is disabled, " +
                                     $"modifier {gameObject.GetPath()}:{GetType().Name} will start disabled too", this);
                }
                catch (Exception)
                {
                    // ignored
                }

                return false;
            }

            return true;
        }

        /// <summary>
        /// jumps to current state (skips delays)
        /// </summary>
        public void JumpToState()
        {
            lastState = _node.GetActiveState();
        }

        protected virtual void Reset() {
            if (this is ISupportValueFreeze supportValueFreeze) {
                supportValueFreeze.FreezeValue();
            }
        }

        #if UNITY_EDITOR
        public class EditorAnimationContext : IDisposable
        {
            protected Modifier modifier;
            
            public BaseStateNode GetNode() => modifier.GetNode();
            public EditorAnimationContext(Modifier modifier)
            {
                this.modifier = modifier;
            }
            
            public bool IsValid() => modifier != null;
            
            public virtual void OnNodeEnabled()
            {
                modifier.HandleNodeEnabled();
                // force updating at least once
                modifier.ForceTransitionUpdate();
            }

            public virtual void HandleStateChange(int oldState, int newState)
            {
                modifier.HandleStateChange(oldState, newState);
            }

            public virtual bool Refresh()
            {
                modifier.Refresh();
                if (!modifier.IsChanged()) 
                    return false;
                UnityEditor.EditorUtility.SetDirty(modifier);
                return true;
            }

            public virtual void Dispose()
            {
            }
        }
        public virtual EditorAnimationContext GetEditorAnimationContext() => new(this);
        
        public virtual (string, LogType) GetEditorComment() => default;
#endif
    }

}
