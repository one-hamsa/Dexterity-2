using OneHamsa.Dexterity.Visual.Utilities;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class RaycastPressField : BaseField
    {
        [TagSelector] public string tag = "Untagged";
        DexterityRaycastFieldProvider provider;
        private NodeRaycastRouter router;

        protected override void Initialize(Node context)
        {
            base.Initialize(context);

            provider = new DexterityRaycastFieldProvider();
            router = context.GetRaycastRouter();
            router.AddReceiver(provider);
        }

        public override void Finalize(Node context)
        {
            base.Finalize(context);
            router.RemoveReceiver(provider);
            router = null;
            provider = null;
        }

        public override int GetValue() => provider != null && provider.GetPress(tag) ? 1 : 0;
    }
}
