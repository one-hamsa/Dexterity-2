using OneHumus.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    [DefaultExecutionOrder(Manager.modifierExecutionPriority)]
    public abstract class Modifier : MonoBehaviour
    {
        [SerializeField]
        public Node _node;

        [SerializeReference]
        public ITransitionStrategy transitionStrategy;

        [SerializeReference]
        public List<PropertyBase> properties = new List<PropertyBase>();

        public Node node => TryFindNode();

        ListMap<int, PropertyBase> propertiesCache = null;

        protected bool transitionChanged;
        protected int forceTransitionChangeFrames;

        public PropertyBase GetProperty(int stateId)
        {
            // runtime
            if (propertiesCache != null)
                return propertiesCache[stateId];

            // editor
            foreach (var prop in properties)
                if (Manager.instance.GetStateID(prop.state) == stateId)
                    return prop;

            return null;
        }
        public PropertyBase activeProperty => GetProperty(node.activeState);

        public virtual bool supportsFreezeValues => false;
        public virtual void FreezeValues() { }

        protected virtual void HandleStateChange(int oldState, int newState) { }

        [Serializable]
        public abstract class PropertyBase
        {
            public string state;
        }

        protected IDictionary<int, float> transitionState;

        private void Awake()
        {
            propertiesCache = new ListMap<int, PropertyBase>();
            foreach (var prop in properties)
            {
                var id = Manager.instance.GetStateID(prop.state);
                if (id == -1)
                {
                    // those properties are kept serialized in order to maintain history, no biggie
                    continue;
                }
                propertiesCache.Add(id, prop);
            }
        }

        protected virtual void Start()
        {
            HandleStateChange(node.activeState, node.activeState);

            var states = new int[propertiesCache.Count];
            var keys = propertiesCache.Keys.GetEnumerator();
            var i = 0;
            while (keys.MoveNext())
                states[i++] = keys.Current;

            transitionState = transitionStrategy.Initialize(states, node.activeState);
        }
        protected virtual void OnEnable()
        {
            if (!EnsureValidState())
            {
                enabled = false;
                return;
            }

            if ((_node = TryFindNode()) == null)
            {
                Debug.LogWarning($"Node not found for modifier ({gameObject.name})");
                enabled = false;
                return;
            }

            node.onStateChanged += HandleStateChange;

            ForceTransitionUpdate();
        }
        protected virtual void OnDisable()
        {
            node.onStateChanged -= HandleStateChange;
        }

        protected virtual void Update()
        {
            transitionState = transitionStrategy.GetTransition(transitionState,
                node.activeState, Time.time - node.stateChangeTime, out transitionChanged);

            if (forceTransitionChangeFrames > 0)
            {
                forceTransitionChangeFrames--;
                transitionChanged = true;
            }
        }

        Node TryFindNode()
        {
            Node current = _node;
            Transform parent = transform;
            while (current == null && parent != null)
            {
                // include inactive if we're inactive
                if (!gameObject.activeInHierarchy || parent.gameObject.activeInHierarchy)
                    current = parent.GetComponent<Node>();

                parent = parent.parent;
            }

            return current;
        }

        bool EnsureValidState()
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

            if (node.reference.stateFunction.GetStateIDs().Count() != propertiesCache.Count)
            {
                Debug.LogError($"properties count != stateFunction states count", this);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Force updating this modifier's transition (even if the transition function reports it's not needed)
        /// </summary>
        /// <param name="frames">How many frames should the update be forced for</param>
        public void ForceTransitionUpdate(int frames = 1) => forceTransitionChangeFrames = frames;
    }

}
