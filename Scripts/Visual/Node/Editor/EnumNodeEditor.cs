using UnityEditor;

namespace OneHamsa.Dexterity.Visual
{

    [CustomEditor(typeof(EnumNode)), CanEditMultipleObjects]
    public class EnumNodeEditor : DexterityBaseNodeEditor
    {
        public override void OnInspectorGUI() {
            Legacy_OnInspectorGUI();
        }
        
        protected override void ShowFields()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(EnumNode.targetObject)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(EnumNode.targetProperty)));
        }
    }
}
