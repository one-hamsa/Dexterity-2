using System;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    [RequireComponent(typeof(DexterityBaseNode))]
    public class NodePointerEvents : MonoBehaviour, IRaycastReceiver, IReferencesNode
    {
        [State]
        public string hoverState = "Hover";
        [State]
        public string pressedState = "Pressed";
        [State]
        public string disabledState = "Disabled";

        private DexterityBaseNode node;
        private NodeRaycastRouter router;

        private int hoverStateId = StateFunction.emptyStateId;
        private int pressedStateId = StateFunction.emptyStateId;
        private int disabledStateId = StateFunction.emptyStateId;

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

        public DexterityBaseNode GetNode() => GetComponent<DexterityBaseNode>();
    }
}