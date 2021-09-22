using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using UnityEditorInternal;

namespace OneHamsa.Dexterity.Visual
{
    [CustomEditor(typeof(DexteritySettings))]
    public class DexteritySettingsEditor : Editor
    {
		SerializedProperty fieldDefinitions;
		ReorderableList fieldDefinitionsList;

		private void OnEnable()
		{
            fieldDefinitions = serializedObject.FindProperty(nameof(DexteritySettings.fieldDefinitions));

			// Set up the reorderable list       
			fieldDefinitionsList = new ReorderableList(serializedObject, fieldDefinitions, true, true, true, true);

            fieldDefinitionsList.drawElementCallback = DrawListItems; // Delegate to draw the elements on the list
			fieldDefinitionsList.drawHeaderCallback = DrawHeader;
		}

        // Draws the elements on the list
        void DrawListItems(Rect rect, int index, bool isActive, bool isFocused)
        {

            SerializedProperty element = fieldDefinitionsList.serializedProperty.GetArrayElementAtIndex(index);

            var type = element.FindPropertyRelative(nameof(FieldDefinition.type));
            var values = element.FindPropertyRelative(nameof(FieldDefinition.enumValues));
            var name = element.FindPropertyRelative(nameof(FieldDefinition.name));

            //Create a property field and label field for each property. 
            EditorGUI.PropertyField(
                new Rect(rect.x, rect.y, 150, EditorGUIUtility.singleLineHeight),
                name,
                GUIContent.none
            );

            EditorGUI.PropertyField(
                new Rect(rect.x + 155, rect.y, 70, EditorGUIUtility.singleLineHeight),
                type,
                GUIContent.none
            );


            if (type.intValue == (int)Node.FieldType.Enum)
            {
                var origColor = GUI.color;
                GUI.color = values.arraySize == 0 ? Color.red : origColor;
                EditorGUI.LabelField(
                    new Rect(rect.x + 230, rect.y, 150, EditorGUIUtility.singleLineHeight),
                    values.arraySize == 0 ? "Empty!" : $"default: {values.GetArrayElementAtIndex(0).stringValue}"
                );
                GUI.color = origColor;

                if (isFocused || isActive)
                {
                    // draw the options to the layout
                    GUILayout.Label(name.stringValue, EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox("First element is used as default", MessageType.None);
                    EditorGUILayout.PropertyField(values, GUIContent.none);
                }
            }
        }

        //Draws the header
        void DrawHeader(Rect rect)
		{
			EditorGUI.LabelField(rect, "Field Definitions", EditorStyles.boldLabel);
		}

		public override void OnInspectorGUI()
        {
            serializedObject.Update(); // Update the array property's representation in the inspector

            fieldDefinitionsList.DoLayoutList(); // Have the ReorderableList do its work

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(DexteritySettings.globalFloatValues)));

            serializedObject.ApplyModifiedProperties();
        }
    }
}