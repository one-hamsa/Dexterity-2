using UnityEngine;

namespace OneHamsa.Dexterity
{
    public static class FieldExtensions
    {
        public static bool GetBooleanValue(this BaseField field)
        {
            if (field.definition.type != FieldNode.FieldType.Boolean)
            {
                Debug.LogError($"GetBooleanValue: {field.definition.GetName()} is not of type boolean");
                return default;
            }
            return field.value == 1;
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
                    return field.GetEnumValue().ToString();
                default:
                    return field.value.ToString();
            }
        }
    }
}
