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
