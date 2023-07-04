using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    [CustomPropertyDrawer(typeof(FieldAttribute))]
    public class FieldDrawer : PropertyDrawer
    {

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {

            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "Use [Field] with strings.");
                return;
            }

            if (DexteritySettingsProvider.settings == null)
            {
                EditorGUI.LabelField(position, label.text, "Dexterity Settings not found.");
                return;
            }

            var fieldsEnum = DexteritySettingsProvider.settings.fieldDefinitions
                .Select(f => f.name);
            var attr = (FieldAttribute)attribute;
            if (attr.allowNull) {
                fieldsEnum = new[] { "(None)" }.Concat(fieldsEnum);
            }

            if (property.serializedObject.targetObject is IGateContainer gateContainer)
            {
                // get internal fields
                fieldsEnum = fieldsEnum.Concat(gateContainer.GetInternalFieldDefinitions()
                    .Select(fd => fd.GetInternalName()));
            }

            var fields = fieldsEnum.ToArray();

            var prevIndex = Array.IndexOf(fields, property.stringValue);
            if (prevIndex == -1)
            {
                prevIndex = 0;
                if (attr.allowNull) {
                    property.stringValue = null;
                } else {
                    property.stringValue = fields[0];
                }
            }

            EditorGUI.BeginProperty(position, GUIContent.none, property);

            int index = EditorGUI.Popup(position, label.text, prevIndex, fields);

            if (index != prevIndex) {
                if (attr.allowNull && index == 0) {
                    property.stringValue = null;
                } else {
                    property.stringValue = fields[index];
                }
            }
            EditorGUI.EndProperty();
        }
    }
}