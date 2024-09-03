using UnityEditor;

namespace OneHamsa.Dexterity
{

    [CustomEditor(typeof(BindingEnumNode)), CanEditMultipleObjects]
    public class BindingEnumNodeEditor : BaseStateNodeEditor
    {
        public override void OnInspectorGUI() {
            Legacy_OnInspectorGUIWithValidate();
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
    }
}
