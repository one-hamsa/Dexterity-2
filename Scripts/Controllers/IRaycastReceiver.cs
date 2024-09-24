using System.Collections.Generic;

namespace OneHamsa.Dexterity
{
    public interface IRaycastReceiver
    {
        void ReceiveHit(IRaycastController controller, ref IRaycastController.RaycastResult hitResult);
        void ClearHit(IRaycastController controller);

        void Resolve(List<IRaycastReceiver> receivers)
        {
            receivers.Add(this);
        }
        
        bool ShouldRecurseParents() => true;
    }
}
