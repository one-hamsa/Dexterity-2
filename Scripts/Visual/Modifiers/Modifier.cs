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

        public DexterityBaseNode node => TryFindNode();
        public float transitionProgress => GetTransitionProgress(node.activeState);
        public float GetTransitionProgress(int state) => transitionState[state];

        public virtual bool animatableInEditor => true;

        Dictionary<int, PropertyBase> propertiesCache = null;

        public override bool IsChanged()
        {
            return base.IsChanged() || isTransitionDelayed;
        }

        public PropertyBase GetProperty(int stateId)
        {
            // runtime
            if (propertiesCache != null)
            {
                if (!propertiesCache.ContainsKey(stateId))
                {
                    Debug.LogWarning($"property for state = {Core.instance.GetStateAsString(stateId)} not found on Modifier {name}" +
                                     $" (probably states were added to node {node.name} without updating modifier)", this);
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
        public PropertyBase activeProperty => GetProperty(node.activeState);

        public virtual void HandleStateChange(int oldState, int newState)
        {
            lastState = oldState;
        }

        private int[] cachedStates;
        private Dictionary<(DelayDirection, int), TransitionDelay> cachedDelays = new();
        private int lastState = StateFunction.emptyStateId;

        protected override int[] states => cachedStates;
        protected override double deltaTime => node.deltaTime;
        protected override double timeSinceStateChange => node.timeSinceStateChange;

        protected bool isTransitionDelayed =>
            timeSinceStateChange < GetStateDelay(lastState, node.activeState);

        public override int activeState => isTransitionDelayed ? lastState : node.activeState;

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
            HandleStateChange(node.activeState, node.activeState);

            cachedStates = node.GetStateIDs().ToArray();

            CacheDelays();

            InitializeTransitionState();
            Update();
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
            if (!EnsureValidState())
            {
                enabled = false;
                return;
            }

            if ((_node = TryFindNode()) == null)
            {
                Debug.LogError($"Node not found for modifier ({gameObject.name})");
                enabled = false;
                return;
            }
            if (!nodesToModifiers.TryGetValue(node, out var modifiers))
            {
                modifiers = nodesToModifiers[node] = new HashSet<Modifier>();
            }
            modifiers.Add(this);

            if (node.isActiveAndEnabled)
                HandleNodeEnabled();
            else
            {
                node.onEnabled -= HandleNodeEnabled;
                node.onEnabled += HandleNodeEnabled;
            }

            node.onStateChanged -= HandleStateChange;
            node.onStateChanged += HandleStateChange;

            base.OnEnable();
        }
        protected override void OnDisable()
        {
            base.OnDisable();

            if (node != null) {
                node.onEnabled -= HandleNodeEnabled;
                node.onStateChanged -= HandleStateChange;
                if (nodesToModifiers.TryGetValue(node, out var modifiers))
                    modifiers.Remove(this);
            }
        }

        DexterityBaseNode TryFindNode()
        {
            DexterityBaseNode current = _node;
            Transform parent = transform;
            while (current == null && parent != null)
            {
                // include inactive if we're inactive
                if (!gameObject.activeInHierarchy || parent.gameObject.activeInHierarchy)
                    current = parent.GetComponent<DexterityBaseNode>();

                parent = parent.parent;
            }

            return current;
        }

        protected bool EnsureValidState()
        {
            if (node == null)
            {
                Debug.LogError($"Node is null", this);
                return false;
            }

            if (!node.enabled)
            {
                // XXX here comes garbage: unity might set a node to disabled when its gameObject is destroyed
                //. before it is == null, but also call OnEnable on child components. so here we are, not printing
                //. this warning because unity. 
                
                // Debug.LogWarning($"Node {node.gameObject.GetPath()} is disabled, " +
                //                  $"modifier {gameObject.GetPath()}:{GetType().Name} will start disabled too", this);
                return false;
            }

            if (transitionStrategy == null)
            {
                Debug.LogError($"Node {node.gameObject.GetPath()} has no transition strategy assigned", this);
                return false;
            }

            return true;
        }

        /// <summary>
        /// jumps to current state (skips delays)
        /// </summary>
        public void JumpToState()
        {
            lastState = node.activeState;
        }

        protected virtual void Reset() {
            if (this is ISupportValueFreeze supportValueFreeze) {
                supportValueFreeze.FreezeValue();
            }
        }
    }

}
