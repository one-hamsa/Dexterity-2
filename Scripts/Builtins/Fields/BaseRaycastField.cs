using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
    public abstract class BaseRaycastField : UpdateableField
    {
        [TagSelector] public string tag = "Untagged";
        protected RaycastControllerFieldProvider provider;
        private NodeRaycastRouter router;

        protected override void Initialize(FieldNode context)
        {
            base.Initialize(context);
            
            provider = new RaycastControllerFieldProvider();
            router = context.GetRaycastRouter();
            router.AddReceiver(provider);
            
            provider.onChanged += SetPendingUpdate;
            context.onDisabled += provider.ClearAll;
        }
        
        public override void Finalize(FieldNode context)
        {
            router.RemoveReceiver(provider);
            provider.onChanged -= SetPendingUpdate;
            context.onDisabled -= provider.ClearAll;

            base.Finalize(context);
        }

        public override void Update()
        {
            SetValue(GetRaycastValue() ? 1 : 0);
            
            if (value == 1)
                // request another update next frame until cleared
                SetPendingUpdate();
        }
        
        protected abstract bool GetRaycastValue();
    }
}
