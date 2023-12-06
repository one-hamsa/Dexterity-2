using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
    [Preserve]
    public class ConstantField : BaseField
    {
        [FieldValue(nameof(BaseField.relatedFieldName), proxy = true)]
        public int constant;

        public override int GetValue() => constant;
    }
}
