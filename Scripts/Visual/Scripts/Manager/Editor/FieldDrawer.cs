using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
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

            if (Manager.instance == null)
            {
                EditorGUI.LabelField(position, label.text, "Dexterity Manager not found.");
                return;
            }

            var fields = Manager.instance.fieldDefinitions.Select(f => f.name).ToArray();

            var prevIndex = Array.IndexOf(fields, property.stringValue);

            var lblPos = position;
            lblPos.width /= 2;
            EditorGUI.LabelField(lblPos, label.text);

            var popPos = lblPos;
            popPos.x += lblPos.width;
            var index = EditorGUI.Popup(popPos, prevIndex, fields);

            if (index != prevIndex)
                property.stringValue = fields[index];
        }
    }
}