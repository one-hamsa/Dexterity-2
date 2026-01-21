using System;
using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
    [Preserve, Serializable]
    public class RaycastHoverField : BaseRaycastField
    {
        protected override bool GetRaycastValue() => provider.GetHover(tag);
    }
}
