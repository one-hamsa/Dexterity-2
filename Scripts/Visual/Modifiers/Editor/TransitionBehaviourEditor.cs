using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Reflection;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace OneHamsa.Dexterity.Visual
{
    [CustomEditor(typeof(TransitionBehaviour), true)]
    public class TransitionBehaviourEditor : Editor
    {
        bool strategyDefined { get; set; }
        TransitionBehaviour transitionBehaviour { get; set; }
        private static bool advancedFoldout { get; set; }

        private void OnEnable() 
        {
            transitionBehaviour = target as TransitionBehaviour;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var customProps = new List<SerializedProperty>();
            var parent = serializedObject.GetIterator();
            foreach (var prop in Utils.GetVisibleChildren(parent))
            {
                switch (prop.name)
                {
                    case "m_Script":
                        break;

                    case nameof(Modifier.transitionStrategy):
                        var p = serializedObject.FindProperty(nameof(TransitionBehaviour.transitionStrategy));
                        strategyDefined = ShowStrategy(target, p);
                        break;

                    default:
                        // get all custom properties here
                        EditorGUI.BeginChangeCheck();
                        EditorGUILayout.PropertyField(prop, true);
                        break;
                }
            }

            if (!strategyDefined)
            {
                EditorGUILayout.HelpBox("Must select Transition Strategy", MessageType.Error);
            }

            serializedObject.ApplyModifiedProperties();
        }
        
        public static bool ShowStrategy(UnityEngine.Object target, SerializedProperty strategyProp)
        {
            var className = Utils.GetClassName(strategyProp);

            if (!string.IsNullOrEmpty(className))
            {
                foreach (var field in Utils.GetChildren(strategyProp))
                {
                    EditorGUILayout.PropertyField(field);
                }
            }


            var saveStrategy = false;
            var types = TypeCache.GetTypesDerivedFrom<ITransitionStrategy>()
                .Where(t => !t.IsAbstract 
                    && (target is IHasStates || !t.IsDefined(typeof(RequiresStateFunctionAttribute), true)))
                .ToArray();
            var typesNames = types
                .Select(t => t.ToString())
                .ToArray();

            var currentIdx = Array.IndexOf(typesNames, className);
            var fieldIdx = currentIdx;

            if (currentIdx == -1 || (advancedFoldout = EditorGUILayout.Foldout(advancedFoldout, "Advanced")))
            {
                EditorGUI.BeginChangeCheck(); 
                fieldIdx = EditorGUILayout.Popup(strategyProp.displayName, currentIdx,
                    Utils.GetNiceName(typesNames, suffix: "Strategy").ToArray());

                if (EditorGUI.EndChangeCheck())
                {
                    saveStrategy = true;
                }
            }
            if (saveStrategy)
            {
                var type = types[fieldIdx];
                strategyProp.managedReferenceValue = Activator.CreateInstance(type);
            } else if (string.IsNullOrEmpty(strategyProp.managedReferenceFullTypename) 
                       && DexteritySettingsProvider.TryGetSettings() != null
                       && DexteritySettingsProvider.settings.defaultTransitionStrategy != null) {
                strategyProp.managedReferenceValue = DexteritySettingsProvider.settings.CreateDefaultTransitionStrategy();
            }

            return !string.IsNullOrEmpty(className);
        }
    }
}
