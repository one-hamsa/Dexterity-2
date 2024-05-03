using System;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using UnityEditorInternal;

namespace OneHamsa.Dexterity
{
    [CustomEditor(typeof(DexteritySettings))]
    public class DexteritySettingsEditor : Editor
    {
		public override void OnInspectorGUI()
        {
            serializedObject.Update(); // Update the array property's representation in the inspector
            
            var fieldDefsProp = serializedObject.FindProperty(nameof(DexteritySettings.fieldDefinitions));
            EditorGUILayout.PropertyField(fieldDefsProp, true);

            GUILayout.Label("Defaults", EditorStyles.whiteLargeLabel);

            var p = serializedObject.FindProperty(nameof(DexteritySettings.defaultTransitionStrategy));
            var strategyDefined = TransitionBehaviourEditor.ShowStrategy(target, p);

            EditorGUILayout.Space(15);
            
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
                    ModifierEditor.ShowSingleStateFields(null, property, savedProperty.property.GetType(),
                        group.Key.Name + "." + savedProperty.name, menuFunction: Menu);
                    EditorGUI.indentLevel--;
                    
                    EditorGUILayout.Space(5);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}