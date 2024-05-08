using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
    [Preserve]
    public class AndField : BaseField
    {
        [SerializeReference]
        public BaseField first;
        [SerializeReference]
        public BaseField second;

        public override BaseField CreateDeepClone()
        {
            AndField clone = (AndField)base.CreateDeepClone();
            clone.first = first.CreateDeepClone();
            clone.second = second.CreateDeepClone();
            return clone;
        }

        protected override void OnUpstreamsChanged(List<BaseField> upstreams = null)
        {
            base.OnUpstreamsChanged(upstreams);
            
            SetValue(first?.value == 1 && second?.value == 1 ? 1 : 0);
        }

        protected override void Initialize(FieldNode context) {
            base.Initialize(context);

            ClearUpstreamFields();

            AddUpstreamField(first);
            AddUpstreamField(second);
        }
    }
}
