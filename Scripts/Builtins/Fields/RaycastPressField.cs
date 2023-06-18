using OneHamsa.Dexterity.Utilities;

namespace OneHamsa.Dexterity.Builtins
{
    public class RaycastPressField : BaseField
    {
        [TagSelector] public string tag = "Untagged";
        DexterityRaycastFieldProvider provider;
        private NodeRaycastRouter router;

        protected override void Initialize(FieldNode context)
        {
            base.Initialize(context);

            provider = new DexterityRaycastFieldProvider();
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

        public override int GetValue() => context != null && provider.GetPress(tag) ? 1 : 0;
    }
}
