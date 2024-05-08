using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
    [Preserve]
    public class RaycastHoverField : UpdateableField
    {
        [TagSelector] public string tag = "Untagged";
        private RaycastControllerFieldProvider provider;
        private NodeRaycastRouter router;

        protected override void Initialize(FieldNode context)
        {
            base.Initialize(context);
            
            provider = RaycastControllerFieldProvider.Create();
            router = context.GetRaycastRouter();
            router.AddReceiver(provider);
        }
        
        public override void Finalize(FieldNode context)
        {
            router.RemoveReceiver(provider);
            
            base.Finalize(context);
        }

        public override void Update()
        {
            SetValue(provider.GetHover(tag) ? 1 : 0);
        }
    }
}
