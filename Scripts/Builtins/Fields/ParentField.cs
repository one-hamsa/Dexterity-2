using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
    [Preserve]
    public class ParentField : BaseField
    {
        [Field]
        public string fieldName;
        public Transform child;
        public bool negate;

        FieldNode parent;
        int fieldId;

        protected override void OnUpstreamsChanged(List<BaseField> upstreams = null)
        {
            base.OnUpstreamsChanged(upstreams);
            
            if (parent == null)
                SetValue(0, upstreams);
            else
            {
                var v = parent.GetOutputField(fieldId).value;
                SetValue(negate ? (v + 1) % 2 : v, upstreams);
            }
        }
        
        private void RefreshReferences()
        {
            var lastParent = parent;
            var transformParent = child.parent;
            parent = transformParent != null ? transformParent.GetComponentInParent<FieldNode>() : null;
            
            if (lastParent != parent) 
            {
                ClearUpstreamFields();
                if (parent != null)
                    AddUpstreamField(parent.GetOutputField(fieldId));

                // proxy might have changed - re-calculate node's outputs
                context.SetDirty();
            }
        }
        
        protected override void Initialize(FieldNode context)
        {
            base.Initialize(context);

            context.onParentTransformChanged += RefreshReferences;
            context.onEnabled += RefreshReferences;

            fieldId = Database.instance.GetFieldID(fieldName);
            if (child == null)
                child = context.transform;
            RefreshReferences();
        }

        public override void Finalize(FieldNode context)
        {
            base.Finalize(context);

            if (context != null)
            {
                context.onParentTransformChanged -= RefreshReferences;
                context.onEnabled -= RefreshReferences;
            }
        }
    }
}
