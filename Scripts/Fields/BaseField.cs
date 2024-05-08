using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using OneHamsa.Dexterity.Utilities;
using UnityEngine;
using UnityEngine.Pool;

namespace OneHamsa.Dexterity
{
    // will automatically serialize fields under "parameters" to custom editor
    public abstract class BaseField
    {
        public const int emptyFieldValue = -1;
        public const int defaultFieldValue = 0;

        /// <summary>
        /// all the fields this field is dependent upon. 
        /// </summary>
        private HashSet<BaseField> upstreamFields;

        /// <summary>
        /// adds an upstream field
        /// </summary>
        protected void AddUpstreamField(BaseField field)
        {
            if (field == this) {
                UnityEngine.Debug.LogError($"{ToShortString()}: Cannot add self as upstream field, this is probably a bug");
                return;
            }

            if (upstreamFields.Add(field))
            {
                field.onValueChanged += OnUpstreamValueChanged;
                OnUpstreamValueChanged(default);
            }
        }

        /// <summary>
        /// removes an existing upstream field
        /// </summary>
        protected void RemoveUpstreamField(BaseField field)
        {
            if (upstreamFields.Remove(field))
            {
                field.onValueChanged -= OnUpstreamValueChanged;
                OnUpstreamValueChanged(default);
            }
        }
        /// <summary>
        /// clears all upstream fields
        /// </summary>
        protected void ClearUpstreamFields()
        {
            using (ListPool<BaseField>.Get(out var fields))
            {
                fields.AddRange(upstreamFields);

                foreach (var field in fields)
                {
                    if (upstreamFields.Remove(field))
                        field.onValueChanged -= OnUpstreamValueChanged;
                }
            }
            
            OnUpstreamValueChanged(default);
        }

        private void OnUpstreamValueChanged(ValueChangeEvent changeEvent)
        {
            OnUpstreamsChanged(changeEvent.visitedFields);
        }

        /// <summary>
        /// context node
        /// </summary>
        [NonSerialized]
        public FieldNode context;

        /// <summary>
        /// related field name (null if not exist), should be set by editor
        /// </summary>
        public string relatedFieldName;

        /// <summary>
        /// field definition, set on runtime
        /// </summary>
        [NonSerialized]
        public FieldDefinition definition;
        /// <summary>
        /// field definition id, set on runtime
        /// </summary>
        [NonSerialized]
        public int definitionId = -1;

        /// <summary>
        /// true if the field is initialized
        /// </summary>
        [NonSerialized]
        public bool initialized;

        /// <summary>
        /// returns the field this provider relies on. 
        /// </summary>
        public HashSet<BaseField> GetUpstreamFields() => upstreamFields;
        
        private List<Action<ValueChangeEvent>> onValueChangedHandlers;
        public event Action<ValueChangeEvent> onValueChanged
        {
            add
            {
                // lazy init
                onValueChangedHandlers ??= new List<Action<ValueChangeEvent>>();
                if (!onValueChangedHandlers.Contains(value))
                    onValueChangedHandlers.Add(value);
            }
            
            remove => onValueChangedHandlers.Remove(value);
        }
        
        public struct ValueChangeEvent
        {
            public BaseField field;
            public int oldValue;
            public int newValue;

            public List<BaseField> visitedFields;
            
            public bool GetOldValueAsBool() => oldValue != 0;
            public bool GetNewValueAsBool() => newValue != 0;
        }

        /// <summary>
        /// returns the last field value calculated by the provider
        /// </summary>
        public int value { get; private set; } = emptyFieldValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void SetValue(int v, List<BaseField> upstreams = null)
        {
            if (upstreams != null && upstreams.Contains(this))
            {
                #if UNITY_EDITOR
                foreach (var field in upstreams)
                    Debug.Log($"Circular Upstream: {field.ToShortString()} ({field.context.gameObject.GetPath()})", field.context);
                Debug.Log($"Circular Upstream: {ToShortString()} ({context.gameObject.GetPath()})", context);
                #endif
                throw new Exception("Circular dependency detected: " + string.Join(" -> ", upstreams));
            }
            
            var oldValue = value;
            value = v;

            if (oldValue == v)
                return;
            
            if (onValueChangedHandlers == null)
                return;
            
            var leasedUpstreams = false;
            if (upstreams == null)
            {
                upstreams = ListPool<BaseField>.Get();
                leasedUpstreams = true;
            }
            try
            {
                using var _ = ListPool<Action<ValueChangeEvent>>.Get(out var tempHandlers);
                tempHandlers.AddRange(onValueChangedHandlers);
                
                // branch upstreams for each handler
                foreach (var action in tempHandlers)
                {
                    try
                    {
                        using (ListPool<BaseField>.Get(out var branch))
                        {
                            branch.AddRange(upstreamFields);
                            branch.Add(this);
                            action(new ValueChangeEvent
                            {
                                field = this,
                                oldValue = oldValue,
                                newValue = v,
                                visitedFields = branch
                            });
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e, context);
                    }
                }
            }
            finally
            {
                if (leasedUpstreams)
                    ListPool<BaseField>.Release(upstreams);
            }
        }

        public virtual BaseField CreateDeepClone()
        {
             var clone = (BaseField)MemberwiseClone();
             
             // safety
             clone.upstreamFields = null;
             clone.onValueChangedHandlers = null;

             return clone;
        }

        /// <summary>
        /// dispatched by the node when initializing a new field
        /// </summary>
        public void Initialize(FieldNode context, int definitionId)
        {
            this.definitionId = definitionId;
            definition = Database.instance.GetFieldDefinition(definitionId);

            if (definitionId == -1 || string.IsNullOrEmpty(definition.GetName()))
                throw new FieldInitializationException();

            value = emptyFieldValue;
            upstreamFields = new HashSet<BaseField>();
            this.context = context;
            Initialize(context);
            initialized = true;
        }

        /// <summary>
        /// override for custom initialization
        /// </summary>
        protected virtual void Initialize(FieldNode context)
        {
        }

        /// <summary>
        /// dispatched by the node when the field is destroyed
        /// </summary>
        public virtual void Finalize(FieldNode context)
        {
            this.context = null;
        }

        /// <summary>
        /// dispatched when the field should recalculate its value
        /// </summary>
        protected virtual void OnUpstreamsChanged(List<BaseField> upstreams = null) { }

        public override string ToString()
        {
            return $"{ToShortString()} -> {this.GetValueAsString()}";
        }

        public virtual string ToShortString()
        {
            return $"{GetType().Name} ({definition.GetName()})";
        }

        /// <summary>
        /// special exception that is used to handle initialization problems 
        /// (missing references, invalid parameters etc.)
        /// </summary>
        public class FieldInitializationException : Exception { }
    }
}
