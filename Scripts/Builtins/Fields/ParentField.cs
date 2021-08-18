using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class ParentField : BaseField
    {
        public string fieldName;
        public bool updateParentReference;

        Node context = null;
        Node parent = null;

        public override bool isProxy => true;
        public override int GetValue() => parent != null ? parent.GetOutputField(fieldName).GetValue() : 0;

        List<Transform> parentsTransform = new List<Transform>();
        public override void RefreshReferences()
        {
            if (parent == null || updateParentReference)
            {
                {
                    // traverse to check if the chain broke
                    var current = context.transform.parent;

                    // make sure you don't skip the update in case the cache is empty
                    if (current == null || parentsTransform.Count > 0)
                    {
                        var i = 0;
                        while (current != null && i < parentsTransform.Count && current == parentsTransform[i])
                        {
                            i++;
                            current = current.parent;
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
                    while (current != null && (parent == null || current.gameObject != parent.gameObject))
                    {
                        parentsTransform.Add(current);
                        current = current.parent;
                    }
                }
            }

            ClearUpstreamFields();
            if (parent != null)
                AddUpstreamField(parent.GetOutputField(fieldName));
        }

        public override void Initialize(Node context)
        {
            base.Initialize(context);

            this.context = context;
            RefreshReferences();
        }
    }
}
