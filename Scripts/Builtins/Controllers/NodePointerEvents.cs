using System;
using UnityEngine;

namespace OneHamsa.Dexterity.Builtins
{
    [RequireComponent(typeof(BaseStateNode))]
    public class NodePointerEvents : MonoBehaviour, IRaycastReceiver, IReferencesNode
    {
        [State(allowEmpty: true)]
        public string hoverState = "Hover";
        [State(allowEmpty: true)]
        public string pressedState = "Pressed";
        [State(allowEmpty: true)]
        public string disabledState = "Disabled";

        protected BaseStateNode node;
        private NodeRaycastRouter router;

        protected int hoverStateId = StateFunction.emptyStateId;
        protected int pressedStateId = StateFunction.emptyStateId;
        protected int disabledStateId = StateFunction.emptyStateId;

        protected virtual void OnEnable()
        {
            node = GetNode();
            router = node.GetRaycastRouter();
            router.AddReceiver(this);

            if (!string.IsNullOrEmpty(hoverState))
                hoverStateId = Database.instance.GetStateID(hoverState);
            if (!string.IsNullOrEmpty(pressedState))
                pressedStateId = Database.instance.GetStateID(pressedState);
            if (!string.IsNullOrEmpty(disabledState))
                disabledStateId = Database.instance.GetStateID(disabledState);
        }

        public virtual void ReceiveHit(IRaycastController controller, ref IRaycastController.RaycastEvent hitEvent)
        {
            var activeState = node.GetActiveState();
            
            if (activeState == disabledStateId)
                hitEvent.result = IRaycastController.RaycastEvent.Result.CannotAccept;
            else if (activeState == hoverStateId)
                hitEvent.result = IRaycastController.RaycastEvent.Result.CanAccept;
            else if (activeState == pressedStateId)
                hitEvent.result = IRaycastController.RaycastEvent.Result.Accepted;
        }

        public virtual void ClearHit(IRaycastController controller)
        {
        }

        public BaseStateNode GetNode() => GetComponent<BaseStateNode>();
    }
}