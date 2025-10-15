using UnityEditor;

namespace OneHamsa.Dexterity
{

    [CustomEditor(typeof(BindingEnumNode)), CanEditMultipleObjects]
    public class BindingEnumNodeEditor : BaseStateNodeEditor
    {
        public override void OnInspectorGUI() {
            Legacy_OnInspectorGUI();
        }
        
        protected override void ShowFields()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(BindingEnumNode.binding)));
            
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(BindingEnumNode.booleanTrueState)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(BindingEnumNode.booleanFalseState)));
            
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(BindingEnumNode.intMinState)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(BindingEnumNode.intMaxState)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(BindingEnumNode.intOutOfBoundsState)));
        }

        protected override void ShowAutoSyncDisabledWarning()
        {
            var binding = ((BindingEnumNode)target).binding;
            if (!binding.IsValid())
                EditorGUILayout.HelpBox("Binding is not valid, states will not be auto-synced with modifiers", MessageType.Warning);
            else
                base.ShowAutoSyncDisabledWarning();
        }
    }
}
