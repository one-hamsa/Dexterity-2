using System;
using System.Collections.Generic;
using OneHamsa.Dexterity.Utilities;

namespace OneHamsa.Dexterity.Builtins
{
    public abstract class UpdateableField : BaseField
    {
        // field to avoid indirection
        [NonSerialized]
        public bool pendingUpdate;

        
        protected void SetPendingUpdate() => pendingUpdate = true;

        public override void OnNodeEnabled()
        {
            base.OnNodeEnabled();
            Manager.instance.AddUpdateableField(this);
            SetPendingUpdate();
        }

        public override void OnNodeDisabled()
        {
            base.OnNodeDisabled();
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
