using OneHamsa.Dexterity.Visual.Utilities;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class RaycastPressField : BaseField
    {
        [TagSelector] public string tag = "Untagged";
        DexterityRaycastFieldProvider provider = null;

        protected override void Initialize(Node context)
        {
            base.Initialize(context);

            provider = context.gameObject.GetOrAddComponent<DexterityRaycastFieldProvider>();
        }

        // don't destroy on finalize - component might be shared

        public override int GetValue() => (provider && provider.GetPress(tag)) ? 1 : 0;
    }
}
