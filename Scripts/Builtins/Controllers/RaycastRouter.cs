using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    /// <summary>
    /// Sends raycast hits to the specified controller.
    /// </summary>
    public class RaycastRouter : MonoBehaviour, IRaycastReceiver
    {
        private IRaycastReceiver receiver;
        public bool hasReceivers => receiver != null;

        public void SetReceiver(IRaycastReceiver receiver)
        {
            if (this.receiver != null)
                Debug.LogError($"RaycastRouter.SetReceiver() called when receiver was already set.", this);
            this.receiver = receiver;
        }
        public void RemoveReceiver(IRaycastReceiver receiver)
        {
            if (this.receiver != receiver)
                Debug.LogError($"RaycastRouter.RemoveReceiver() called with a receiver that was not set.", this);
            this.receiver = null;
        }
        
        void IRaycastReceiver.ReceiveHit(IRaycastController controller, RaycastHit hit)
        {
            Debug.LogError($"RaycastRouter.ReceiveHit() should never be called.", this);
        }
        
        void IRaycastReceiver.ClearHit(IRaycastController controller)
        {
            Debug.LogError($"RaycastRouter.ClearHit() should never be called.", this);
        }

        public IRaycastReceiver Resolve() => receiver;
    }
}
