using System.Collections.Generic;
using OneHamsa.Dexterity.Utilities;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    public class NodeRaycastRouter : MonoBehaviour, IRaycastReceiver
    {
        private readonly HashSet<IRaycastReceiver> receivers = new();
        private readonly List<RaycastRouter> routers = new();
        
        public void AddReceiver(IRaycastReceiver receiver) 
        {
            receivers.Add(receiver);
        }
        
        public void RemoveReceiver(IRaycastReceiver receiver) 
        {
            receivers.Remove(receiver);
        }

        void IRaycastReceiver.ReceiveHit(IRaycastController controller, ref IRaycastController.RaycastEvent raycastEvent)
        {
            foreach (var receiver in receivers) {
                receiver.ReceiveHit(controller, ref raycastEvent);
            }
        }

        void IRaycastReceiver.ClearHit(IRaycastController controller)
        {
            foreach (var receiver in receivers) {
                receiver.ClearHit(controller);
            }
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

                if (current.GetComponent<FieldNode>() != null) {
                    continue;
                }
                
                if (current != transform) {
                    var collider = current.GetComponent<Collider>();
                    if (collider != null) {
                        // add router component and route it to receivers
                        var router = collider.gameObject.GetOrAddComponent<RaycastRouter>();
                        router.SetReceiver(this);
                        routers.Add(router);
                    }
                }
            }
        }
        private void RemoveRoutersFromAllColliders() {
            foreach (var router in routers) {
                router.RemoveReceiver(this);
                Destroy(router);
            }
            routers.Clear();
        }
    }
}