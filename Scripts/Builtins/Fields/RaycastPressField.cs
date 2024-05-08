using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
    [Preserve]
    public class RaycastPressField : UpdateableField
    {
        [TagSelector] public string tag = "Untagged";
        public bool stayPressedOutOfBounds = false;
        private RaycastControllerFieldProvider provider;
        private NodeRaycastRouter router;

        protected override void Initialize(FieldNode context)
        {
            base.Initialize(context);

            provider = RaycastControllerFieldProvider.Create();
            provider.stayPressedOutOfBounds = stayPressedOutOfBounds;
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
            SetValue(provider.GetPress(tag) ? 1 : 0);
        }
    }
}
