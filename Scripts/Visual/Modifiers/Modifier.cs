using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using OneHamsa.Dexterity.Visual.Utilities;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
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
        
        private static Dictionary<DexterityBaseNode, HashSet<Modifier>> nodesToModifiers = new();
        public static IEnumerable<Modifier> GetModifiers(DexterityBaseNode node)
        {
            if (nodesToModifiers.TryGetValue(node, out var modifiers))
                return modifiers;
            return System.Linq.Enumerable.Empty<Modifier>();
        }

        [SerializeField]
        public DexterityBaseNode _node;

        [SerializeReference]
        public List<PropertyBase> properties = new();
        
        [SerializeField]
        public List<TransitionDelay> delays = new();

        [HideInInspector] public List<string> lastSeenStates = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DexterityBaseNode GetNode() => TryFindNode();
        public float transitionProgress => GetTransitionProgress(GetNode().GetActiveState());
        public float GetTransitionProgress(int state)
        {
            if (transitionState == null)
                return default;
            transitionState.TryGetValue(state, out var result);
            return result;
        }

        public virtual bool animatableInEditor => true;

        private Dictionary<int, PropertyBase> propertiesCache = null;
        private bool foundNode;

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
                    Debug.LogWarning($"property for state = {Core.instance.GetStateAsString(stateId)} not found on Modifier {name}" +
                                     $" (probably states were added to node {GetNode().name} without updating modifier)", this);
                    // just return first
                    foreach (var p in propertiesCache.Values)
                        return p;
                }
                return propertiesCache[stateId];
            }

            // editor
            foreach (var prop in properties)
                if (Core.instance.GetStateID(prop.state) == stateId)
                    return prop;

            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PropertyBase GetActiveProperty() => GetProperty(GetNode().GetActiveState());

        public virtual void HandleStateChange(int oldState, int newState)
        {
            lastState = oldState;
        }

        private int[] cachedStates;
        private Dictionary<(DelayDirection, int), TransitionDelay> cachedDelays = new();
        private int lastState = StateFunction.emptyStateId;

        protected override int[] states => cachedStates;
        protected override double deltaTime => GetNode().deltaTime;
        protected override double timeSinceStateChange => GetNode().timeSinceStateChange;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool IsTransitionDelayed() => timeSinceStateChange < GetStateDelay(lastState, GetNode().GetActiveState());
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetActiveState() => IsTransitionDelayed() ? lastState : GetNode().GetActiveState();

        [Serializable]
        public abstract class PropertyBase
        {
            public string state;
        }

        public override void Awake()
        {
            propertiesCache = new Dictionary<int, PropertyBase>();
            foreach (var prop in properties)
            {
                if (prop == null) {
                    Debug.LogError("Null property in Modifier", this);
                    continue;
                }
                var id = Core.instance.GetStateID(prop.state);
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
                propertiesCache.Add(id, prop);
            }
        }
        public virtual void OnDestroy() 
        {

        }

        public virtual void HandleNodeEnabled()
        {
            var activeState = GetNode().GetActiveState();
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
                cachedDelays[(delay.direction, Core.instance.GetStateID(delay.state))] = delay;
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
            // wait for node to be enabled
            if (!GetNode().isActiveAndEnabled)
                return;
            
            base.Refresh();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        DexterityBaseNode TryFindNode()
        {
            if (Application.isPlaying && foundNode) 
                return _node;
            
            DexterityBaseNode current = _node;
            Transform parent = transform;
            while (current == null && parent != null)
            {
                // include inactive if we're inactive
                if (!gameObject.activeInHierarchy || parent.gameObject.activeInHierarchy)
                    current = parent.GetComponent<DexterityBaseNode>();

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
    }

}
