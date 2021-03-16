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
        public override int GetValue() => parent.GetOutputField(FieldName).GetValue();

        Transform[] parentsTransform;

        public override void RefreshReferences()
        {
            if (parent == null || UpdateParentReference)
            {
                if (parent != null)
                {
                    // traverse to check if the chain broke
                    var current = context.transform.parent;
                    var i = 0;
                    while (current != null && i < parentsTransform.Length && current == parentsTransform[i])
                    {
                        i++;
                        current = current.parent;
                    }
                    if (current != null && i == parentsTransform.Length)
                        // no need to update
                        return;
                }

                // save new references
                parent = context.transform.parent.GetComponentInParent<Node>();
                var currentParents = new List<Transform>();
                {
                    var current = context.transform.parent;
                    while (current.gameObject != parent.gameObject)
                    {
                        currentParents.Add(current);
                        current = current.parent;
                    }
                }
                parentsTransform = currentParents.ToArray();
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
