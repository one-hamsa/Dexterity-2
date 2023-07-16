using UnityEditor;

namespace OneHamsa.Dexterity
{

    [CustomEditor(typeof(ObjectSourceEnumNode)), CanEditMultipleObjects]
    public class ObjectSourceEnumNodeEditor : BaseStateNodeEditor
    {
        public override void OnInspectorGUI() {
            Legacy_OnInspectorGUI();
        }
        
        protected override void ShowFields()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(ObjectSourceEnumNode.targetObject)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(ObjectSourceEnumNode.targetProperty)));
            
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(ObjectSourceEnumNode.booleanTrueState)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(ObjectSourceEnumNode.booleanFalseState)));
        }
    }
}
