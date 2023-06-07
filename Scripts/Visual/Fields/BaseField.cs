using System;
using System.Collections.Generic;

namespace OneHamsa.Dexterity.Visual
{
    // will automatically serialize fields under "parameters" to custom editor
    public abstract class BaseField
    {
        /// <summary>
        /// all the fields this field is dependent upon. 
        /// initialized once to save performance
        /// </summary>
        private readonly HashSet<BaseField> upstreamFields = new();

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
            upstreamFields.Clear();

            Manager.instance.SetDirty(this);
        }

        /// <summary>
        /// context node
        /// </summary>
        [NonSerialized]
        public Node context;

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

        /// <summary>
        /// dispatched by the node when initializing a new field
        /// </summary>
        public void Initialize(Node context, int definitionId)
        {
            this.definitionId = definitionId;
            definition = Database.instance.GetFieldDefinition(definitionId);

            if (definitionId == -1 || string.IsNullOrEmpty(definition.name))
                throw new FieldInitializationException();

            this.context = context;
            Initialize(context);
            initialized = true;
        }
        /// <summary>
        /// override for custom initialization
        /// </summary>
        protected virtual void Initialize(Node context) { }

        /// <summary>
        /// dispatched by the node when the field is destroyed
        /// </summary>
        public virtual void Finalize(Node context) { }

        /// <summary>
        /// dispatched by the graph before updating
        /// </summary>
        public virtual void RefreshReferences() { }

        /// <summary>
        /// dispatched by the graph when value can be cached
        /// </summary>
        public virtual void CacheValue() { }

        /// <summary>
        /// dispatched manually when field needs structure recalculation (finding scene references, etc.)
        /// </summary>
        public virtual void RebuildCache() { }

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
