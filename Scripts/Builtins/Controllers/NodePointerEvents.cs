using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OneHamsa.Dexterity.Builtins
{
    [RequireComponent(typeof(BaseStateNode))]
    public class NodePointerEvents : MonoBehaviour, IRaycastReceiver, IReferencesNode
    {
        private static Queue<Transform> workQueue = new();
        
        [State(allowEmpty: true)]
        public string[] hoverStates = { "Hover" };
        [State(allowEmpty: true)]
        public string[] pressedStates = { "Pressed" };
        [State(allowEmpty: true)]
        public string[] disabledStates = { "Disabled" };

        public bool recurseNodes = true;

        protected List<BaseStateNode> nodes;
        // private HashSet<NodeRaycastRouter> routers = new();

        protected int[] hoverStateIds;
        protected int[] pressedStateIds;
        protected int[] disabledStateIds;

        protected virtual void OnEnable()
        {
            nodes = GetNodesInChildrenRecursive().ToList();
            
            foreach (var node in nodes)
            {
                // var router = node.GetRaycastRouter();
                // router.AddReceiver(this);
                // routers.Add(router);
            }

            hoverStateIds = hoverStates.Select(Database.instance.GetStateID).ToArray();
            pressedStateIds = pressedStates.Select(Database.instance.GetStateID).ToArray();
            disabledStateIds = disabledStates.Select(Database.instance.GetStateID).ToArray();
        }
        
        protected virtual void OnDisable()
        {
            // foreach (var router in routers)
            //     router.RemoveReceiver(this);
            // routers.Clear();
        }

        protected IRaycastController.RaycastEvent.Result GetResultFromState()
        {
            var result = IRaycastController.RaycastEvent.Result.Default;
            foreach (var node in nodes)
            {
                var activeState = node.GetActiveState();

                if (Array.IndexOf(pressedStateIds, activeState) != -1)
                {
                    result = IRaycastController.RaycastEvent.Result.Accepted;
                    break;
                }
                if (Array.IndexOf(hoverStateIds, activeState) != -1)
                    result = IRaycastController.RaycastEvent.Result.CanAccept;
                else if (Array.IndexOf(disabledStateIds, activeState) != -1 
                         && result == IRaycastController.RaycastEvent.Result.Default)
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

        public BaseStateNode GetNode() => GetComponent<BaseStateNode>();
        private IEnumerable<BaseStateNode> GetNodesInChildrenRecursive()
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
                var node = transform.GetComponent<BaseStateNode>();
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