using UnityEngine;

namespace OneHamsa.Dexterity.Builtins
{
    public abstract class UpdateableField : BaseField
    {
        private FieldProvider provider;

        protected override void Initialize(FieldNode context)
        {
            base.Initialize(context);
            
            provider = context.gameObject.AddComponent<FieldProvider>();
            provider.hideFlags = HideFlags.HideAndDontSave;
            provider.field = this;
        }
        
        public override void Finalize(FieldNode context)
        {
            base.Finalize(context);
            
            if (provider != null)
                UnityEngine.Object.Destroy(provider);
        }
        
        public abstract void Update();
    }
    
    // HACK: Generic MonoBehaviours are not supported, so we have to use a non-generic MonoBehaviour for the update hook
    //. (otherwise could use UpdateableField<T> : BaseField where T : UpdateableField<T>)
    internal class FieldProvider : MonoBehaviour
    {
        public UpdateableField field;
        private void Update() => field?.Update();
    }
}
