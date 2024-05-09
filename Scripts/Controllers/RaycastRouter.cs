using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// Sends raycast hits to the specified controller.
    /// </summary>
    public class RaycastRouter : MonoBehaviour, IRaycastReceiver
    {
        public List<GameObject> manualReceivers = new();
        public bool dontRecurse;

        private void OnEnable()
        {
            foreach (var go in manualReceivers)
            {
                foreach (var raycastReceiver in go.GetComponents<IRaycastReceiver>())
                    AddReceiver(raycastReceiver);
            }
        }
        
        private void OnDisable()
        {
            foreach (var go in manualReceivers)
            {
                foreach (var raycastReceiver in go.GetComponents<IRaycastReceiver>())
                    RemoveReceiver(raycastReceiver);
            }
        }

        private HashSet<IRaycastReceiver> receivers = new();

        public void AddReceiver(IRaycastReceiver receiver)
        {
            if (receiver == null) {
                Debug.LogError("Trying to add a null receiver!");
                return;
            }
            
            receivers.Add(receiver);
        }
        public void RemoveReceiver(IRaycastReceiver receiver)
        {
            receivers.Remove(receiver);
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
