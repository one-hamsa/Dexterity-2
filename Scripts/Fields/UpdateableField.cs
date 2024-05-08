using System;
using System.Collections.Generic;

namespace OneHamsa.Dexterity.Builtins
{
    public abstract class UpdateableField : BaseField
    {
        // field to avoid indirection
        [NonSerialized]
        public bool pendingUpdate;
        
        protected void SetPendingUpdate() => pendingUpdate = true;

        protected override void Initialize(FieldNode context)
        {
            base.Initialize(context);
            
            Manager.instance.AddUpdateableField(this);
            SetPendingUpdate();
        }
        
        public override void Finalize(FieldNode context)
        {
            base.Finalize(context);
            
            if (Manager.instance != null)
                Manager.instance.RemoveUpdateableField(this);
        }
        
        public abstract void Update();

		protected override void OnUpstreamsChanged(List<BaseField> upstreams = null)
		{
			base.OnUpstreamsChanged(upstreams);
			
			SetPendingUpdate();
		}
    }
}
