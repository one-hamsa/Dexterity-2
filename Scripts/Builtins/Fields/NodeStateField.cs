using System.Collections.Generic;
using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
    [Preserve]
    public class NodeStateField : BaseField
    {
        public BaseStateNode targetNode;
        [State(objectFieldName: nameof(targetNode))]
        public string targetState;
        public bool negate;

        private int targetStateId;
        
        public override BaseField CreateDeepClone()
        {
            throw new System.Data.DataException($"Attempting to DeepClone field of type {GetType()} - this is not allowed");
        }

        protected override void Initialize(FieldNode context)
        {
            base.Initialize(context);
            
            targetStateId = Database.instance.GetStateID(targetState);
        }

        public override void OnNodeEnabled()
        {
            base.OnNodeEnabled();
            targetNode.onStateChanged += OnTargetNodeStateChanged;
            targetNode.onEnabled += SetValueAccordingToNodeState;
            targetNode.onDisabled += SetValueAccordingToNodeState;
            
            if (targetNode.initialized)
                OnTargetNodeStateChanged(0, targetNode.GetActiveState());
        }

        public override void OnNodeDisabled()
        {
            base.OnNodeDisabled();
            if (targetNode != null)
            {
                targetNode.onStateChanged -= OnTargetNodeStateChanged;
                targetNode.onEnabled -= SetValueAccordingToNodeState;
                targetNode.onDisabled -= SetValueAccordingToNodeState;
            }
        }

        private void SetValueAccordingToNodeState()
        {
            var v = targetNode.GetActiveState() == targetStateId ? 1 : 0;
            SetValue(negate ? (v + 1) % 2 : v);
        }

        private void OnTargetNodeStateChanged(int oldState, int newState)
        {
            SetValueAccordingToNodeState();
        }
    }
}
