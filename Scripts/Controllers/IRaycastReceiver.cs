using System.Collections.Generic;

namespace OneHamsa.Dexterity
{
    public interface IRaycastReceiver
    {
        void ReceiveHit(IRaycastController controller, ref IRaycastController.RaycastEvent hitEvent);
        void ClearHit(IRaycastController controller);

        void Resolve(List<IRaycastReceiver> receivers)
        {
            receivers.Add(this);
        }
        
        bool ShouldRecurseParents() => true;
    }
}
