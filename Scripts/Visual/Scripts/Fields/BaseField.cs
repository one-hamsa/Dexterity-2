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
        private readonly HashSet<BaseField> upstreamFields = new HashSet<BaseField>();

        /// <summary>
        /// adds an upstream field
        /// </summary>
        protected void AddUpstreamField(BaseField field)
        {
            upstreamFields.Add(field);
            Manager.instance.SetDirty();
        }
        /// <summary>
        /// removes an existing upstream field
        /// </summary>
        protected void RemoveUpstreamField(BaseField field)
        {
            upstreamFields.Remove(field);
            Manager.instance.SetDirty();
        }
        /// <summary>
        /// clears all upstream fields
        /// </summary>
        protected void ClearUpstreamFields()
        {
            upstreamFields.Clear();
            Manager.instance.SetDirty();
        }

        /// <summary>
        /// related field name (null if not exist), should be set by editor
        /// </summary>
        public string relatedFieldName;

        /// <summary>
        /// is the field a dependency itself, or is it only reflecting another field?
        /// </summary>
        public virtual bool proxy { get; protected set; } = false;

        /// <summary>
        /// returns the field this provider relies on. 
        /// returned set should be treated as read-only (XXX maybe this should be in enforced)
        /// </summary>
        public HashSet<BaseField> GetUpstreamFields() => upstreamFields;

        /// <summary>
        /// returns the field value calculated by the provider
        /// </summary>
        public abstract int GetValue();

        /// <summary>
        /// dispatched by the node when initializing a new field
        /// </summary>
        public virtual void Initialize(Node context) { }

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
