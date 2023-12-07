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

        FieldNode parent = null;
        int fieldId;

        List<Transform> parentsTransform;

        // only proxy when parent is found
        public override bool proxy => parent != null;

        public override int GetValue()
        {
            if (parent == null)
                return 0;

            var value = parent.GetOutputField(fieldId).GetValue();
            return negate ? (value + 1) % 2 : value;
        }

        public override void RefreshReferences()
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

            parentsTransform = new();
            context.onParentTransformChanged += RefreshReferences;

            fieldId = Database.instance.GetFieldID(fieldName);
            if (child == null)
                child = context.transform;
            RefreshReferences();
        }
    }
}
