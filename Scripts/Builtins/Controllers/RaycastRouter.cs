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
        private readonly List<IRaycastReceiver> receivers = new List<IRaycastReceiver>();

        public void AddReceiver(IRaycastReceiver receiver)
        {
            receivers.Add(receiver);
        }
        
        void IRaycastReceiver.ClearHit(IRaycastController controller)
        {
            foreach (var receiver in receivers)
                receiver.ClearHit(controller);
        }

        void IRaycastReceiver.ReceiveHit(IRaycastController controller, RaycastHit hit)
        {
            foreach (var receiver in receivers)
                receiver.ReceiveHit(controller, hit);
        }
    }
}
