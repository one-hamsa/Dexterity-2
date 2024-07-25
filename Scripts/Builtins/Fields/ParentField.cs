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
        // TODO: using child as Transform doesn't give us an API to know when/if this child is moved in hierarchy
        //       So it might lead to stale parent references in case this child is moved to be under a different node
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
            fieldId = Database.instance.GetFieldID(fieldName);
            base.Initialize(context);
        }

        public override void OnNodeEnabled()
        {
            base.OnNodeEnabled();
            if (child == null)
            {
                context.onParentTransformChanged += RefreshReferences;
                context.onEnabled += RefreshReferences;
                child = context.transform;
            }
            else
            {
                // TODO: We cannot detect hierarchy changes when child is explicitly set!
            }
            
            RefreshReferences();
        }

        public override void OnNodeDisabled()
        {
            base.OnNodeDisabled();
            if (context != null)
            {
                context.onParentTransformChanged -= RefreshReferences;
                context.onEnabled -= RefreshReferences;
            }
        }
    }
}
