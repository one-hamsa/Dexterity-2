using OneHumus;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class RaycastPressField : BaseField
    {
        DexterityRaycastFieldProvider provider = null;

        public override void Initialize(Node context)
        {
            base.Initialize(context);

            provider = context.gameObject.GetOrAddComponent<DexterityRaycastFieldProvider>();
        }

        // don't destroy on finalize - component might be shared

        public override int GetValue() => (provider && provider.press) ? 1 : 0;
    }
}
