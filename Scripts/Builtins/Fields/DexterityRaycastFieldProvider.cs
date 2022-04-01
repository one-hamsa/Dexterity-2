using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    internal class DexterityRaycastFieldProvider : MonoBehaviour, IRaycastReceiver
    {
        HashSet<IRaycastController> controllers = new HashSet<IRaycastController>();
        HashSet<IRaycastController> receivedPressStart = new HashSet<IRaycastController>();
        List<RaycastRouter> routers = new List<RaycastRouter>();

        public bool GetHover(string castTag = null) 
        {
            foreach (var ctrl in controllers) {
                if (!string.IsNullOrEmpty(castTag) && ctrl.tag != castTag)
                    continue;
                return true;
            }
            return false;
        }
        public bool GetPress(string castTag = null)
        {
                foreach (var ctrl in controllers) {
                    if (!string.IsNullOrEmpty(castTag) && ctrl.tag != castTag)
                        continue;

                    if (ctrl.isPressed && receivedPressStart.Contains(ctrl))
                        return true;
                }
                return false;
        }

        public void ReceiveHit(IRaycastController controller, RaycastHit hit)
        {
            controllers.Add(controller);
            if (controller.wasPressedThisFrame)
                receivedPressStart.Add(controller);
        }

        public void ClearHit(IRaycastController controller)
        {
            controllers.Remove(controller);
            receivedPressStart.Remove(controller);
        }

        private void Awake() {
            AddRoutersToAllColliders();
        }

        private void OnDestroy() {
            RemoveRoutersFromAllColliders();
        }

        private void AddRoutersToAllColliders() {
            var queue = new Queue<Transform>();
            queue.Enqueue(transform);

            while (queue.Count > 0) {
                var current = queue.Dequeue();

                foreach (Transform child in current) {
                    queue.Enqueue(child);
                }

                if (current.GetComponent<Node>() != null) {
                    continue;
                }
                
                if (current != transform) {
                    var collider = current.GetComponent<Collider>();
                    if (collider != null) {
                        // add router component and route it to receivers
                        var router = collider.gameObject.GetOrAddComponent<RaycastRouter>();
                        router.AddReceiver(this);
                        routers.Add(router);
                    }
                }
            }
        }
        private void RemoveRoutersFromAllColliders() {
            foreach (var router in routers) {
                router.RemoveReceiver(this);
                if (!router.hasReceivers)
                    Destroy(router);
            }
            routers.Clear();
        }
    }
}