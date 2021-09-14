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
        private readonly List<BaseField> upstreamFields = new List<BaseField>();

        /// <summary>
        /// adds an upstream field
        /// </summary>
        protected void AddUpstreamField(BaseField field)
        {
            if (!upstreamFields.Contains(field))
                upstreamFields.Add(field);

            Manager.instance.SetDirty(field);
            Manager.instance.SetDirty(this);
        }
        /// <summary>
        /// removes an existing upstream field
        /// </summary>
        protected void RemoveUpstreamField(BaseField field)
        {
            if (upstreamFields.Contains(field))
                upstreamFields.Remove(field);

            Manager.instance.SetDirty(field);
            Manager.instance.SetDirty(this);
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
        /// related field name (null if not exist), should be set by editor
        /// </summary>
        public string relatedFieldName;

        /// <summary>
        /// field definition, set on runtime
        /// </summary>
        public FieldDefinition definition { get; private set; }
        /// <summary>
        /// field definition id, set on runtime
        /// </summary>
        public int definitionId { get; private set; }

        /// <summary>
        /// is the field a dependency itself, or is it only reflecting another node's outputs?
        /// </summary>
        public virtual bool proxy { get; protected set; } = false;

        /// <summary>
        /// returns the field this provider relies on. 
        /// returned set should be treated as read-only (XXX maybe this should be in enforced)
        /// </summary>
        public IEnumerable<BaseField> GetUpstreamFields() => upstreamFields;

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
            definition = Manager.instance.GetFieldDefinition(definitionId);

            if (definitionId == -1 || string.IsNullOrEmpty(definition.name))
                throw new FieldInitializationException();

            Initialize(context);
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
        /// should the node field show in the list of available node fields?
        /// </summary>
        public static bool showInInspector = true;

        /// <summary>
        /// special exception that is used to handle initialization problems 
        /// (missing references, invalid parameters etc.)
        /// </summary>
        public class FieldInitializationException : Exception { }
    }
}
