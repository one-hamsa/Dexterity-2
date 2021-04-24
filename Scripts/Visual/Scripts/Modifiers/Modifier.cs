using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    [DefaultExecutionOrder(Manager.ModifierExecutionPriority)]
    public abstract class Modifier : MonoBehaviour
    {
        [SerializeField]
        protected Node node;

        [SerializeField]
        protected StateFunction stateFunction;
        public StateFunction StateFunction => stateFunction;

        [SerializeReference]
        protected ITransitionStrategy transitionStrategy;

        // TODO validate this is marked!
        [SerializeField]
        [HideInInspector]
        protected string defaultState;
        protected string lastState { get; private set; }
        public string ActiveState => lastState;

        [SerializeReference]
        protected List<PropertyBase> properties = new List<PropertyBase>();
        public IEnumerable<PropertyBase> Properties => properties.ToArray();

        Dictionary<string, PropertyBase> propertiesCache = null;
        public PropertyBase GetProperty(string state)
        {
            // runtime
            if (propertiesCache != null)
                return propertiesCache[state];

            // editor
            foreach (var prop in properties)
                if (prop.State == state)
                    return prop;

            return null;
        }
        public PropertyBase ActiveProperty => GetProperty(ActiveState);
        protected virtual void HandleStateChange() { }

        [Serializable]
        public abstract class PropertyBase
        {
            public string State;
        }

        protected Dictionary<string, float> transitionState;

        private void Awake()
        {
            propertiesCache = properties.ToDictionary(p => p.State);
        }

        protected virtual void Start()
        {
            TryFindNode();

            if (string.IsNullOrEmpty(defaultState))
            {
                defaultState = properties[0].State;
                Debug.LogWarning($"no default state selected, selecting first ({defaultState})", this);
            }

            lastState = defaultState;
            HandleStateChange();

            transitionState = transitionStrategy.Initialize(properties.Select(p => p.State).ToArray(), lastState);
            ForceTransitionUpdate();

            RegisterOutputEvents();
        }

        void TryFindNode()
        {
            if (!node)
                node = GetComponentInParent<Node>();

            if (!node)
            {
                Debug.LogWarning($"Node not found for modifier ({gameObject.name})");
                enabled = false;
            }
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

            TryFindNode();
            RegisterOutputEvents();
        }

        private void RegisterOutputEvents()
        {
            isDirty = true;
            outputFields.Clear();
            foreach (var f in stateFunction.GetFields())
            {
                outputFields.Add(node.GetOutputField(f));
            }
            foreach (var field in outputFields)
            {
                field.OnValueChanged += MarkStateDirty;
            }
        }

        private void MarkStateDirty(Node.OutputField field, int oldValue, int newValue) => isDirty = true;
        protected virtual void OnDisable()
        {
            isDirty = true;
            foreach (var field in outputFields)
            {
                field.OnValueChanged -= MarkStateDirty;
            }
        }

        protected bool transitionChanged;
        protected int forceTransitionChangeFrames;
        public void ForceTransitionUpdate(int frames = 1) => forceTransitionChangeFrames = frames;

        protected virtual void Update()
        {
            string newState;
            if (isDirty)
            {
                newState = GetState();
                if (newState == null)
                {
                    Debug.LogWarning($"{name}: got null for new state, not updating");
                    return;
                }
                if (newState != lastState)
                {
                    stateChangeTime = Time.time;
                    lastState = newState;
                    HandleStateChange();
                }
                isDirty = false;
            }
            else
            {
                newState = lastState;
            }
            transitionState = transitionStrategy.GetTransition(transitionState,
                newState, Time.time - stateChangeTime, out transitionChanged);

            if (forceTransitionChangeFrames > 0)
            {
                forceTransitionChangeFrames--;
                transitionChanged = true;
            }
        }

        Dictionary<string, int> fields = new Dictionary<string, int>();
        private Dictionary<string, int> GetFields()
        {
            fields.Clear();
            foreach (var field in outputFields)
            {
                var value = field.GetValue();
                // if this field isn't provided just assume default
                if (value == Node.EMPTY_FIELD_VALUE)
                {
                    value = Node.DEFAULT_FIELD_VALUE;
                }
                fields[field.name] = value;
            }
            return fields;
        }
        protected string GetState() => stateFunction.Evaluate(GetFields());
    }

}
