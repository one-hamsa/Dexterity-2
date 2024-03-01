using System.Collections.Generic;
using OneHamsa.Dexterity.Utilities;
using UnityEngine;
using UnityEngine.Pool;

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
            foreach (var receiver in receivers)
            {
                if (receiver is MonoBehaviour { isActiveAndEnabled: false })
                    continue;
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

        private void AddRoutersToAllColliders()
        {
            using var _ = ListPool<Transform>.Get(out var queue);
            using var __ = ListPool<IRaycastReceiver>.Get(out var thisObjectReceivers);
            GetComponents(thisObjectReceivers);
            
            queue.Add(transform);

            while (queue.Count > 0) {
                var current = queue[0];
                queue.RemoveAt(0);

                // TODO wrong order
                foreach (Transform child in current) 
                {
                    queue.Add(child);
                }
                
                if (current != transform) {
                    var collider = current.GetComponent<Collider>();
                    if (collider != null) {
                        // add router component and route it to receivers
                        var router = collider.gameObject.GetComponent<RaycastRouter>();
                        if (router == null)
                        {
                            router = collider.gameObject.AddComponent<RaycastRouter>();
                            router.hideFlags = HideFlags.HideInInspector | HideFlags.HideAndDontSave;
                        }

                        // add this, and all other receivers on this object, to the router
                        for (int i = 0; i < thisObjectReceivers.Count; i++) 
                        {
                            if (thisObjectReceivers[i] is MonoBehaviour { enabled: false } or RaycastRouter)
                                continue;
                            router.AddReceiver(thisObjectReceivers[i]);
                        }
                        routers.Add(router);
                    }
                }
            }
        }
        private void RemoveRoutersFromAllColliders() {
            using var __ = ListPool<IRaycastReceiver>.Get(out var thisObjectReceivers);
            GetComponents(thisObjectReceivers);
            
            foreach (var router in routers)
            {
                if (router == null)
                    continue;
                
                for (int i = 0; i < thisObjectReceivers.Count; i++) 
                {
                    if (thisObjectReceivers[i] is MonoBehaviour { enabled: false } or RaycastRouter)
                        continue;
                    router.RemoveReceiver(thisObjectReceivers[i]);
                }
                
                if ((router.hideFlags & HideFlags.HideAndDontSave) != 0)
                    Destroy(router);
            }
            routers.Clear();
        }
    }
}