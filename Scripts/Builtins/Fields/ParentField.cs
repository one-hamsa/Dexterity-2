using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class ParentField : BaseField
    {
        [Field]
        public string fieldName;
        public bool negate;
        public bool updateParentReference;

        Node context = null;
        Node parent = null;
        int fieldId;

        // only proxy when parent is found
        public override bool proxy => parent != null;

        public override int GetValue()
        {
            if (parent == null)
                return 0;

            var value = parent.GetOutputField(fieldId).GetValue();
            return negate ? (value + 1) % 2 : value;
        }

        List<Transform> parentsTransform = new List<Transform>();
        public override void RefreshReferences()
        {
            if (parent == null || updateParentReference)
            {
                {
                    // traverse to check if the chain broke
                    var current = context.transform.parent;

                    // make sure you don't skip the update in case the cache is empty
                    if (parentsTransform.Count > 0)
                    {
                        var i = 0;
                        while (i < parentsTransform.Count && current == parentsTransform[i])
                        {
                            i++;
                            current = current?.parent;
                        }
                        if (i == parentsTransform.Count)
                            // no need to update
                            return;
                    }
                }

                // save new references
                parent = context.transform.parent.GetComponentInParent<Node>();
                parentsTransform.Clear();

                {
                    var current = context.transform.parent;
                    // save until the parent, or until the root if the parent is null
                    do
                    {
                        parentsTransform.Add(current);
                        current = current?.parent;
                    }
                    while (current != null && (parent == null || current.gameObject != parent.gameObject));
                }
            }

            ClearUpstreamFields();
            if (parent != null)
                AddUpstreamField(parent.GetOutputField(fieldId));

            // proxy might have changed - re-calculate node's outputs
            context.SetDirty();
        }

        protected override void Initialize(Node context)
        {
            base.Initialize(context);

            fieldId = Manager.instance.GetFieldID(fieldName);
            this.context = context;
            RefreshReferences();
        }
    }
}
