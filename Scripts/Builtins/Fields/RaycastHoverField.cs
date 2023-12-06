using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
    [Preserve]
    public class RaycastHoverField : BaseField
    {
        [TagSelector] public string tag = "Untagged";
        DexterityRaycastFieldProvider provider;
        private NodeRaycastRouter router;

        protected override void Initialize(FieldNode context)
        {
            base.Initialize(context);

            if (router != null)
                UnityEngine.Debug.LogError("Twice?");
            
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

        public override int GetValue() => provider != null && provider.GetHover(tag) ? 1 : 0;
    }
}
