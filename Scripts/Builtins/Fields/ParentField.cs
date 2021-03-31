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
                if (parent != null)
                {
                    // traverse to check if the chain broke
                    var current = context.transform.parent;
                    var i = 0;
                    while (current != null && i < parentsTransform.Count && current == parentsTransform[i])
                    {
                        i++;
                        current = current.parent;
                    }
                    if (current != null && i == parentsTransform.Count && i > 0)
                        // no need to update
                        return;
                }

                // save new references
                parent = context.transform.parent.GetComponentInParent<Node>();
                parentsTransform.Clear();

                if (parent != null)
                {
                    var current = context.transform.parent;
                    while (current.gameObject != parent.gameObject)
                    {
                        parentsTransform.Add(current);
                        current = current.parent;
                    }
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
