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

        public override bool GetValue() {
            if (first == null || second == null)
                return false;
            return first.GetValue() && second.GetValue();
        }

        protected override void Initialize(FieldNode context) {
            base.Initialize(context);

            ClearUpstreamFields();

            AddUpstreamField(first);
            AddUpstreamField(second);
        }
    }
}
