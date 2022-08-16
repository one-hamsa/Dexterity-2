using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
                    Debug.LogWarning($"property for state = {Core.instance.GetStateAsString(stateId)} not found", this);
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
        protected override double currentTime => node.currentTime;
        protected override double stateChangeTime => node.stateChangeTime;

        protected bool isTransitionDelayed =>
            currentTime - stateChangeTime < GetStateDelay(lastState, node.activeState);

        public override int activeState => isTransitionDelayed ? lastState : node.activeState;

        [Serializable]
        public abstract class PropertyBase
        {
            public string state;
        }

        public override void Awake()
        {
            if (propertiesCache != null)
                // this can be called manually by modifier managers that work even when node is inactive
                return;
            
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
                propertiesCache.Add(id, prop);
            }
        }
        public virtual void OnDestroy() 
        {

        }

        public virtual void HandleNodeEnabled()
        {
            HandleStateChange(node.activeState, node.activeState);

            cachedStates = new int[propertiesCache.Count];
            var keys = propertiesCache.Keys.GetEnumerator();
            var i = 0;
            while (keys.MoveNext())
                states[i++] = keys.Current;

            CacheDelays();

            InitializeTransitionState();
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
                Debug.LogError("Node is null", this);
                return false;
            }

            if (!node.enabled)
            {
                Debug.LogError("Node is disabled", this);
                return false;
            }

            if (transitionStrategy == null)
            {
                Debug.LogError("No transition strategy assigned", this);
                return false;
            }

            return true;
        }

        protected virtual void Reset() {
            if (this is ISupportValueFreeze supportValueFreeze) {
                supportValueFreeze.FreezeValue();
            }
        }
    }

}