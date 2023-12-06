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

        public override int GetValue()
        {
            var value = targetNode.GetActiveState() == targetStateId ? 1 : 0;
            return negate ? (value + 1) % 2 : value;
        }

        protected override void Initialize(FieldNode context)
        {
            base.Initialize(context);
            
            targetStateId = Database.instance.GetStateID(targetState);
        }
    }
}
