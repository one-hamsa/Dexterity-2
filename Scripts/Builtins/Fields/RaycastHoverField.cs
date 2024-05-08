using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
    [Preserve]
    public class RaycastHoverField : BaseRaycastField
    {
        protected override bool GetRaycastValue() => provider.GetHover(tag);
    }
}
