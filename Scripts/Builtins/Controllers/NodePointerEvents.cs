using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    [RequireComponent(typeof(DexterityBaseNode))]
    public class NodePointerEvents : MonoBehaviour, IRaycastReceiver, IReferencesNode
    {
        private static Queue<Transform> workQueue = new();
        
        [State(allowEmpty: true)]
        public string hoverState = "Hover";
        [State(allowEmpty: true)]
        public string pressedState = "Pressed";
        [State(allowEmpty: true)]
        public string disabledState = "Disabled";

        public bool recurseNodes = true;

        protected List<DexterityBaseNode> nodes;
        private NodeRaycastRouter router;

        protected int hoverStateId = StateFunction.emptyStateId;
        protected int pressedStateId = StateFunction.emptyStateId;
        protected int disabledStateId = StateFunction.emptyStateId;

        protected virtual void OnEnable()
        {
            nodes = GetNodesInChildrenRecursive().ToList();
            
            foreach (var node in nodes)
            {
                router = node.GetRaycastRouter();
                router.AddReceiver(this);
            }

            if (!string.IsNullOrEmpty(hoverState))
                hoverStateId = Database.instance.GetStateID(hoverState);
            if (!string.IsNullOrEmpty(pressedState))
                pressedStateId = Database.instance.GetStateID(pressedState);
            if (!string.IsNullOrEmpty(disabledState))
                disabledStateId = Database.instance.GetStateID(disabledState);
        }

        protected IRaycastController.RaycastEvent.Result GetResultFromState()
        {
            var result = IRaycastController.RaycastEvent.Result.Default;
            foreach (var node in nodes)
            {
                var activeState = node.GetActiveState();

                if (activeState == pressedStateId)
                {
                    result = IRaycastController.RaycastEvent.Result.Accepted;
                    break;
                }
                if (activeState == hoverStateId)
                    result = IRaycastController.RaycastEvent.Result.CanAccept;
                else if (activeState == disabledStateId && result == IRaycastController.RaycastEvent.Result.Default)
                    result = IRaycastController.RaycastEvent.Result.CannotAccept;
            }
            
            return result;
        }

        public virtual void ReceiveHit(IRaycastController controller, ref IRaycastController.RaycastEvent hitEvent)
        {
            var result = GetResultFromState();
            if (result != IRaycastController.RaycastEvent.Result.Default)
                hitEvent.result = result;
        }

        public virtual void ClearHit(IRaycastController controller)
        {
        }

        public DexterityBaseNode GetNode() => GetComponent<DexterityBaseNode>();
        private IEnumerable<DexterityBaseNode> GetNodesInChildrenRecursive()
        {
            yield return GetNode();
            
            if (!recurseNodes)
                yield break;

            // all children, but stop recursing when finding nodes
            workQueue.Clear();
            workQueue.Enqueue(this.transform);

            while (workQueue.Count > 0) 
            {
                var transform = workQueue.Dequeue();
                var node = transform.GetComponent<DexterityBaseNode>();
                if (transform != this.transform && node != null)
                {
                    // only return nodes that don't have a NodePointerEvents component
                    if (node.GetComponent<NodePointerEvents>() == null)
                        yield return node;
                }
                else
                    foreach (Transform child in transform)
                        workQueue.Enqueue(child);
            }
        }
    }
}