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

        public int activeState { get; private set; } = -1;

        [SerializeReference]
        public List<PropertyBase> properties = new List<PropertyBase>();

        public Node node => TryFindNode();
        public StateFunctionGraph stateFunction => node.reference.stateFunction;

        ListMap<int, PropertyBase> propertiesCache = null;
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
        public StateFunctionGraph activeStateFunction { get; private set; }
        public PropertyBase activeProperty => GetProperty(activeState);
        protected virtual void HandleStateChange() { }

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
            if ((_node = TryFindNode()) == null)
            {
                Debug.LogWarning($"Node not found for modifier ({gameObject.name})");
                enabled = false;
                return;
            }

            var defaultStateId = Manager.instance.GetStateID(node.initialState);
            if (defaultStateId == -1)
            {
                defaultStateId = Manager.instance.GetStateID(properties[0].state);
                Debug.LogWarning($"no default state selected, selecting first ({properties[0].state})", this);
            }

            activeState = defaultStateId;
            HandleStateChange();

            var states = new int[propertiesCache.Count];
            var keys = propertiesCache.Keys.GetEnumerator();
            var i = 0;
            while (keys.MoveNext())
                states[i++] = keys.Current;

            transitionState = transitionStrategy.Initialize(states, activeState);
            ForceTransitionUpdate();

            RegisterOutputEvents();
        }

        Node TryFindNode()
        {
            Node current = _node;
            if (current == null)
                current = GetComponentInParent<Node>();

            return current;
        }

        float stateChangeTime;
        bool isDirty = true;

        bool EnsureValidState()
        {
            if (stateFunction == null)
            {
                Debug.LogWarning("No state function assigned", this);
                return false;
            }
            
            activeStateFunction = Manager.instance.GetActiveStateFunction(stateFunction);
            if (activeStateFunction == null)
            {
                Debug.LogError($"stateFunction {stateFunction.name} not found in Manager", this);
                return false;
            }

            if (activeStateFunction.GetStateIDs().Count() != propertiesCache.Count)
            {
                Debug.LogError($"properties count != stateFunction states count", this);
                return false;
            }

            if (transitionStrategy == null)
            {
                Debug.LogWarning("No transition strategy assigned", this);
                return false;
            }

            return true;
        }

        List<Node.OutputField> outputFields = new List<Node.OutputField>();
        protected virtual void OnEnable()
        {
            if (!EnsureValidState())
            {
                enabled = false;
                return;
            }

            _node = TryFindNode();
            RegisterOutputEvents();
        }

        private void RegisterOutputEvents()
        {
            isDirty = true;
            outputFields.Clear();
            foreach (var f in activeStateFunction.GetFieldIDs())
            {
                outputFields.Add(node.GetOutputField(f));
            }
            foreach (var field in outputFields)
            {
                field.onValueChanged += MarkStateDirty;
            }
        }

        private void MarkStateDirty(Node.OutputField field, int oldValue, int newValue) => isDirty = true;
        protected virtual void OnDisable()
        {
            isDirty = true;
            foreach (var field in outputFields)
            {
                field.onValueChanged -= MarkStateDirty;
            }
        }

        protected bool transitionChanged;
        protected int forceTransitionChangeFrames;
        private FieldsState fieldsState = new FieldsState(32);
        public void ForceTransitionUpdate(int frames = 1) => forceTransitionChangeFrames = frames;

        protected virtual void Update()
        {
            int newState;
            if (isDirty)
            {
                newState = GetState();
                if (newState == -1)
                {
                    Debug.LogWarning($"{name}: got -1 for new state, not updating");
                    return;
                }
                if (newState != activeState)
                {
                    stateChangeTime = Time.time;
                    activeState = newState;
                    HandleStateChange();
                }
                isDirty = false;
            }
            else
            {
                newState = activeState;
            }
            transitionState = transitionStrategy.GetTransition(transitionState,
                newState, Time.time - stateChangeTime, out transitionChanged);

            if (forceTransitionChangeFrames > 0)
            {
                forceTransitionChangeFrames--;
                transitionChanged = true;
            }
        }

        private FieldsState GetFields()
        {
            fieldsState.Clear();

            foreach (var field in outputFields)
            {
                var value = field.GetValue();
                // if this field isn't provided just assume default
                if (value == Node.emptyFieldValue)
                {
                    value = Node.defaultFieldValue;
                }
                fieldsState.Add((field.definitionId, value));
            }
            return fieldsState;
        }
        protected int GetState() => activeStateFunction.Evaluate(GetFields());
    }

}
