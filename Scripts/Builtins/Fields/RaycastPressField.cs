using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
    [Preserve]
    public class RaycastPressField : BaseField
    {
        [TagSelector] public string tag = "Untagged";
        public bool stayPressedOutOfBounds = false;
        DexterityRaycastFieldProvider provider;
        private NodeRaycastRouter router;

        protected override void Initialize(FieldNode context)
        {
            base.Initialize(context);

            provider = new DexterityRaycastFieldProvider();
            provider.stayPressedOutOfBounds = stayPressedOutOfBounds;
            router = context.GetRaycastRouter();
            router.AddReceiver(provider);
        }

        public override void Finalize(FieldNode context)
        {
            base.Finalize(context);
            router.RemoveReceiver(provider);
            router = null;
            provider = null;
        }

        public override int GetValue() => provider != null && provider.GetPress(tag) ? 1 : 0;
    }
}
