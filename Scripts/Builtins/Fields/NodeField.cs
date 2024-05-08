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
        
        public override BaseField CreateDeepClone()
        {
            throw new System.Data.DataException($"Attempting to DeepClone field of type {GetType()} - this is not allowed");
        }

        protected override void OnUpstreamsChanged(List<BaseField> upstreams)
        {
            base.OnUpstreamsChanged(upstreams);
            
            var v = GetValueBeforeNegation();
            SetValue(negate ? (v + 1) % 2 : v);
        }

        private int GetValueBeforeNegation() {
            switch (takeValueWhen) {
                case TakeValueWhen.AnyEqualsTrue:
                    foreach (var field in outputFields)
                    {
                        if (!field.node.initialized || !field.node.isActiveAndEnabled)
                            continue;
                        
                        if (field.GetBooleanValue())
                            return 1;
                    }
                    return 0;
                case TakeValueWhen.AnyEqualsFalse:
                    foreach (var field in outputFields) {
                        if (field.node.initialized && field.node.isActiveAndEnabled && field.GetBooleanValue())
                            return 0;
                    }
                    return 1;
                case TakeValueWhen.AllEqual:
                    int? prevValue = null;
                    foreach (var field in outputFields) {
                        var value = !field.node.initialized || !field.node.isActiveAndEnabled ? 0 : field.value;
                        if (prevValue.HasValue && prevValue.Value != value)
                            return 0;

                        prevValue = value;
                    }
                    return prevValue.HasValue ? prevValue.Value : 0;
            }
            return 0;
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
