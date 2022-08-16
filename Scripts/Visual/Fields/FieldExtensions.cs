using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    public static class FieldExtensions
    {
        public static bool GetBooleanValue(this BaseField field)
        {
            if (field.definition.type != Node.FieldType.Boolean)
            {
                Debug.LogError($"GetBooleanValue: {field.definition.name} is not of type boolean");
                return default;
            }
            return field.GetValue() == 1;
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