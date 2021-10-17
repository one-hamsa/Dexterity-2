using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class NodeField : BaseField
    {
        public Node targetNode;
        [Field]
        public string fieldName;
        public bool negate;

        Node.OutputField outputField;

        // we always report about someone else's field, so mark as proxy
        public override bool proxy => true;
        public override int GetValue() => negate ? (outputField.GetValue() + 1) % 2 : outputField.GetValue();

        protected override void Initialize(Node context)
        {
            base.Initialize(context);

            if (targetNode == null)
            {
                Debug.LogError($"target node == null", context);
                throw new FieldInitializationException();
            }

            outputField = targetNode.GetOutputField(fieldName);
            
            ClearUpstreamFields();
            AddUpstreamField(outputField);
        }
    }
}
