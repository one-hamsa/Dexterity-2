using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
    [Preserve]
    public class RaycastPressField : BaseRaycastField
    {
        public bool stayPressedOutOfBounds = false;

        protected override void Initialize(FieldNode context)
        {
            base.Initialize(context);

            provider.stayPressedOutOfBounds = stayPressedOutOfBounds;
        }

        protected override bool GetRaycastValue() => provider.GetPress(tag);
    }
}
