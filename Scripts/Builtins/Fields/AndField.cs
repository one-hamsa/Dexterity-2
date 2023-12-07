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
            AndField clone = base.CreateDeepClone() as AndField;
            clone.first = first.CreateDeepClone();
            clone.second = second.CreateDeepClone();
            return clone;
        }

        public override int GetValue() {
            return first?.GetValue() == 1 && second?.GetValue() == 1 ? 1 : 0;
        }

        protected override void Initialize(FieldNode context) {
            base.Initialize(context);

            ClearUpstreamFields();

            AddUpstreamField(first);
            AddUpstreamField(second);
        }
    }
}
