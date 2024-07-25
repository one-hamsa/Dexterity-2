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
            provider = new RaycastControllerFieldProvider();
            router = context.GetRaycastRouter();
            provider.onChanged += SetPendingUpdate;
            
            base.Initialize(context);
        }
        
        public override void Uninitialize(FieldNode context)
        {
            provider.onChanged -= SetPendingUpdate;

            base.Uninitialize(context);
        }

        public override void OnNodeEnabled()
        {
            base.OnNodeEnabled();
            router.AddReceiver(provider);
        }

        public override void OnNodeDisabled()
        {
            base.OnNodeDisabled();
            router.RemoveReceiver(provider);
            provider.ClearAll();
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
