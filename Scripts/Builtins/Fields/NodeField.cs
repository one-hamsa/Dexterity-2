using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class NodeField : BaseField
    {
        public Node TargetNode;
        public string FieldName;        

        Node.OutputField outputField;
        bool isNegated = false;

        public override bool isProxy => true;
        public override int GetValue() => isNegated ? (outputField.GetValue() + 1) % 2 : outputField.GetValue();

        public override void Initialize(Node context)
        {
            base.Initialize(context);

            string fieldName = FieldName;
            isNegated = FieldName[0] == '!';
            if (isNegated) {
                fieldName = fieldName.Substring(1);
            }
            outputField = TargetNode.GetOutputField(fieldName);
            
            ClearUpstreamFields();
            AddUpstreamField(outputField);
        }
    }
}
