using System.Collections.Generic;
using OneHamsa.Dexterity.Utilities;
using UnityEngine;

namespace OneHamsa.Dexterity.Builtins
{
    public static class NodeExtensions
    {
        public static NodeRaycastRouter GetRaycastRouter(this BaseStateNode node)
        {
            return node.GetOrAddComponent<NodeRaycastRouter>();
        }
        
        public static void AddUpstream(this Node.OutputField field, Node.OutputField other, 
            NodeReference.Gate.OverrideType overrideType = NodeReference.Gate.OverrideType.Additive)
        {
            field.node.enabled = false;
            field.node.AddGate(new NodeReference.Gate
            {
                outputFieldName = field.definition.name,
                overrideType = overrideType,
                field = new NodeField
                {
                    targetNodes = new List<Node> { other.node },
                    fieldName = other.definition.name
                }
            });
            field.node.enabled = true;
        }
        public static string GetEnumValue(this BaseField field)
        {
            if (field.definition.type != Node.FieldType.Enum)
            {
                Debug.LogError($"GetEnumValue: {field.definition.name} is not of type enum");
                return null;
            }
            var value = field.GetValue();
            if (value == Node.emptyFieldValue)
                value = 0;

            return field.definition.enumValues[value];
        }

        public static string GetValueAsString(this BaseField field) {
            if (field.GetValue() == Node.emptyFieldValue)
                return "(empty)";

            switch (field.definition.type) {
                case Node.FieldType.Boolean:
                    return field.GetBooleanValue().ToString();
                case Node.FieldType.Enum:
                    return field.GetEnumValue().ToString();
                default:
                    return field.GetValue().ToString();
            }
        }
    }
}
