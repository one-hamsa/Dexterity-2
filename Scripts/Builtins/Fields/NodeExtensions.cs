using System.Collections.Generic;
using OneHamsa.Dexterity.Utilities;
using UnityEngine;

namespace OneHamsa.Dexterity.Builtins
{
    public static class NodeExtensions
    {
        public static NodeRaycastRouter GetRaycastRouter(this BaseStateNode node)
        {
            var router = node.GetOrAddComponent<NodeRaycastRouter>();
            router.hideFlags = HideFlags.HideInInspector | HideFlags.HideAndDontSave;
            return router;
        }
        
        public static void AddUpstream(this FieldNode.OutputField field, FieldNode.OutputField other, 
            NodeReference.Gate.OverrideType overrideType = NodeReference.Gate.OverrideType.Additive,
            bool negate = false)
        {
            field.node.enabled = false;
            field.node.AddGate(new NodeReference.Gate
            {
                outputFieldName = Database.instance.GetStateAsString(field.stateId),
                overrideType = overrideType,
                field = new NodeField
                {
                    targetNodes = new List<FieldNode> { other.node },
                    fieldName = Database.instance.GetStateAsString(field.stateId),
                    negate = negate
                }
            });
            field.node.enabled = true;
        }
    }
}
