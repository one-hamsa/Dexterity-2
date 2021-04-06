using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class ParentField : BaseField
    {
        public string FieldName;
        public bool UpdateParentReference;

        Node context = null;
        Node parent = null;

        public override bool isProxy => true;
        public override int GetValue() => parent != null ? parent.GetOutputField(FieldName).GetValue() : 0;

        List<Transform> parentsTransform = new List<Transform>();
        public override void RefreshReferences()
        {
            if (parent == null || UpdateParentReference)
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
                AddUpstreamField(parent.GetOutputField(FieldName));
        }

        public override void Initialize(Node context)
        {
            base.Initialize(context);

            this.context = context;
            RefreshReferences();
        }
    }
}
