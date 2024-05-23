using System.Collections.Generic;
using OneHamsa.Dexterity.Utilities;
using UnityEngine;

namespace OneHamsa.Dexterity.Builtins
{
    public static class NodeExtensions
    {
        public static NodeRaycastRouter GetRaycastRouter(this BaseStateNode node)
        {
            var existing = node.GetComponent<NodeRaycastRouter>();
            if (existing)
                return existing;
            
            var router = node.gameObject.AddComponent<NodeRaycastRouter>();
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
                outputFieldName = field.definition.GetName(),
                overrideType = overrideType,
                field = new NodeField
                {
                    targetNodes = new List<FieldNode> { other.node },
                    fieldName = other.definition.GetName(),
                    negate = negate
                }
            });
            field.node.enabled = true;
        }
        public static string GetEnumValue(this BaseField field)
        {
            if (field.definition.type != FieldNode.FieldType.Enum)
            {
                Debug.LogError($"GetEnumValue: {field.definition.GetName()} is not of type enum");
                return null;
            }
            var value = field.value;
            if (value == BaseField.emptyFieldValue)
                value = 0;

            return field.definition.enumValues[value];
        }

        public static string GetValueAsString(this BaseField field) {
            if (field.value == BaseField.emptyFieldValue)
                return "(empty)";

            switch (field.definition.type) {
                case FieldNode.FieldType.Boolean:
                    return field.GetBooleanValue().ToString();
                case FieldNode.FieldType.Enum:
                    return field.GetEnumValue();
                default:
                    return field.value.ToString();
            }
        }
    }
}
