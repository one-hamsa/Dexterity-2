using System.Collections.Generic;

namespace OneHamsa.Dexterity.Builtins
{
    public class NodeField : BaseField
    {
        public enum TakeValueWhen
        {
            AnyEqualsTrue = 0,
            AnyEqualsFalse = 1,
            AllEqual = 2,
        }

        public List<FieldNode> targetNodes;
        [Field]
        public string fieldName;
        public TakeValueWhen takeValueWhen = TakeValueWhen.AnyEqualsTrue;
        public bool negate;

        List<FieldNode.OutputField> outputFields;

        // we always report about someone else's field, so mark as proxy
        public override bool proxy => true;
        
        public override BaseField CreateDeepClone()
        {
            throw new System.Data.DataException($"Attempting to DeepClone field of type {GetType()} - this is not allowed");
        }
        
        public override bool GetValue() {
            var value = GetValueBeforeNegation();
            return negate ? !value : value;
        }

        private bool GetValueBeforeNegation() {
            switch (takeValueWhen) {
                case TakeValueWhen.AnyEqualsTrue:
                    foreach (var field in outputFields)
                    {
                        if (field.node == null || !field.node.isActiveAndEnabled)
                            continue;
                        
                        if (field.GetValue())
                            return true;
                    }
                    return false;
                case TakeValueWhen.AnyEqualsFalse:
                    foreach (var field in outputFields) {
                        if (field.node != null && field.node.isActiveAndEnabled && field.GetValue())
                            return false;
                    }
                    return true;
                case TakeValueWhen.AllEqual:
                    bool? prevValue = null;
                    foreach (var field in outputFields) {
                        var value = field.node != null && field.node.isActiveAndEnabled && field.GetValue();
                        if (prevValue.HasValue && prevValue.Value != value)
                            return false;

                        prevValue = value;
                    }
                    return prevValue.HasValue && prevValue.Value;
            }
            return false;
        }

        protected override void Initialize(FieldNode context)
        {
            base.Initialize(context);

            outputFields = new();

            if (targetNodes.Count == 0)
                targetNodes.Add(context);

            ClearUpstreamFields();
            foreach (var node in targetNodes) {
                if (node == null)
                    continue;

                FieldNode.OutputField outputField = node.GetOutputField(fieldName);
                outputFields.Add(outputField);
                AddUpstreamField(outputField);
            }
        }
    }
}
