using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace OneHamsa.Dexterity
{
    // will automatically serialize fields under "parameters" to custom editor
    public abstract class BaseField
    {
        /// <summary>
        /// all the fields this field is dependent upon. 
        /// initialized once to save performance
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

            if (upstreamFields.Add(field)) {
                Manager.instance.SetDirty(field);
                Manager.instance.SetDirty(this);
            }
        }
        /// <summary>
        /// removes an existing upstream field
        /// </summary>
        protected void RemoveUpstreamField(BaseField field)
        {
            if (upstreamFields.Remove(field)) {
                Manager.instance.SetDirty(field);
                Manager.instance.SetDirty(this);
            }
        }
        /// <summary>
        /// clears all upstream fields
        /// </summary>
        protected void ClearUpstreamFields()
        {
            foreach (var field in upstreamFields)
                Manager.instance.SetDirty(field);
            upstreamFields.Clear();

            Manager.instance.SetDirty(this);
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
        /// is the field a dependency itself, or is it only reflecting another node's outputs?
        /// </summary>
        public virtual bool proxy { get; protected set; } = false;

        /// <summary>
        /// returns the field this provider relies on. 
        /// </summary>
        public HashSet<BaseField> GetUpstreamFields() => upstreamFields;

        /// <summary>
        /// returns the field value calculated by the provider
        /// </summary>
        public abstract int GetValue();

        public virtual BaseField CreateDeepClone()
        {
             return MemberwiseClone() as BaseField;
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

            this.context = context;
            Initialize(context);
            initialized = true;
        }

        /// <summary>
        /// override for custom initialization
        /// </summary>
        protected virtual void Initialize(FieldNode context)
        {
            upstreamFields = HashSetPool<BaseField>.Get();
        }

        /// <summary>
        /// dispatched by the node when the field is destroyed
        /// </summary>
        public virtual void Finalize(FieldNode context)
        {
            HashSetPool<BaseField>.Release(upstreamFields);
        }

        /// <summary>
        /// dispatched by the graph before updating
        /// </summary>
        public virtual void RefreshReferences() { }

        /// <summary>
        /// dispatched by the graph when value can be cached
        /// </summary>
        public virtual void CacheValue() { }

        public override string ToString()
        {
            return $"{ToShortString()} -> {this.GetValueAsString()}";
        }

        public virtual string ToShortString() {
            return $"{GetType().Name}";
        }

        /// <summary>
        /// special exception that is used to handle initialization problems 
        /// (missing references, invalid parameters etc.)
        /// </summary>
        public class FieldInitializationException : Exception { }
    }
}
