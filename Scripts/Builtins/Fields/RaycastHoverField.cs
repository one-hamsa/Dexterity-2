using OneHumus;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class RaycastHoverField : BaseField
    {
        DexterityRaycastFieldProvider provider = null;

        public override void Initialize(Node context)
        {
            base.Initialize(context);

            provider = context.gameObject.GetOrAddComponent<DexterityRaycastFieldProvider>();
        }

        // don't destroy on finalize - component might be shared

        public override int GetValue() => (provider && provider.hover) ? 1 : 0;
    }
}
