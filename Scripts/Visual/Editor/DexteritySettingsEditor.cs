using System;
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


            GUILayout.Label("Defaults", EditorStyles.whiteLargeLabel);

            var p = serializedObject.FindProperty(nameof(DexteritySettings.defaultTransitionStrategy));
            var strategyDefined = TransitionBehaviourEditor.ShowStrategy(target, p);

            EditorGUILayout.Space(15);

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(DexteritySettings.repeatHitCooldown)));
            
            var namedProperties = serializedObject.FindProperty(nameof(DexteritySettings.namedProperties));
            var settings = (DexteritySettings)target;

            var namedProps = new List<(SerializedProperty serializedProperty, DexteritySettings.SavedProperty savedProperty)>();
            for (var i = 0; i < namedProperties.arraySize; ++i)
            {
                var prop = settings.namedProperties[i];
                namedProps.Add((namedProperties.GetArrayElementAtIndex(i).Copy(), prop));

                
            }
            
            if (namedProperties.arraySize > 0)
            {
                EditorGUILayout.Space(15);
                GUILayout.Label("Linked Properties", EditorStyles.whiteLargeLabel);
            }

            foreach (var group in namedProps
                         .GroupBy(p => p.savedProperty.property.GetType().DeclaringType)
                         .OrderBy(g => g.Key.Name))
            {
                // get outer class
                var monoScript = FindFirstObjectByType(group.Key);
                if (monoScript == null)
                    monoScript = MonoScript.FromScriptableObject(settings);
                var icon = EditorGUIUtility.ObjectContent(monoScript, monoScript.GetType());
                icon.text = ObjectNames.NicifyVariableName(group.Key.Name);
                var origColor = GUI.color;
                GUI.color = new Color(1f, .6f, .7f, 1f);
                EditorGUILayout.LabelField(icon);
                GUI.color = origColor;
                
                foreach (var (serializedProperty, savedProperty) in group)
                {
                    void Menu()
                    {
                        var menu = new GenericMenu();
                        menu.AddItem(new GUIContent("Remove"), false, () =>
                        {
                            settings.namedProperties.Remove(savedProperty);
                            serializedObject.Update();
                        });
                        menu.ShowAsContext();
                    }
                    
                    var property = serializedProperty.FindPropertyRelative(nameof(DexteritySettings.SavedProperty.property));
                    GUI.contentColor = Color.yellow;
                    EditorGUILayout.PropertyField(serializedProperty.FindPropertyRelative(nameof(DexteritySettings.SavedProperty.name)));
                    GUI.contentColor = origColor;
                    EditorGUI.indentLevel++;
                    ModifierEditor.ShowSingleStateFields(property, savedProperty.property.GetType(),
                        group.Key.Name + "." + savedProperty.name, menuFunction: Menu);
                    EditorGUI.indentLevel--;
                    
                    EditorGUILayout.Space(5);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}