using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// Sends raycast hits to the specified controller.
    /// </summary>
    public class RaycastRouter : MonoBehaviour, IRaycastReceiver
    {
        private HashSet<IRaycastReceiver> receivers = new();

        public void AddReceiver(IRaycastReceiver receiver)
        {
            if (!receivers.Add(receiver))
                Debug.LogError($"RaycastRouter.SetReceiver() called when receiver was already added.", this);
        }
        public void RemoveReceiver(IRaycastReceiver receiver)
        {
            if (!receivers.Remove(receiver))
                Debug.LogError($"RaycastRouter.RemoveReceiver() called with a receiver that was not added.", this);
        }
        
        void IRaycastReceiver.ReceiveHit(IRaycastController controller, ref IRaycastController.RaycastEvent hitEvent)
        {
            Debug.LogError($"RaycastRouter.ReceiveHit() should never be called.", this);
        }
        
        void IRaycastReceiver.ClearHit(IRaycastController controller)
        {
            Debug.LogError($"RaycastRouter.ClearHit() should never be called.", this);
        }

        void IRaycastReceiver.Resolve(List<IRaycastReceiver> receivers)
        {
            receivers.AddRange(this.receivers);
        }
    }
}
