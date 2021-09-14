using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    public static class FieldExtensions
    {
        public static bool GetBooleanValue(this BaseField field)
        {
            return field.GetValue() == 1;
        }
        public static string GetEnumValue(this BaseField field)
        {
            if (field.definition.type != Node.FieldType.Enum)
            {
                Debug.LogError($"GetEnumValue: {field.definition.name} is not of type enum");
                return null;
            }
            return field.definition.enumValues[field.GetValue()];
        }
    }
}