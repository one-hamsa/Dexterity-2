using UnityEditor;

namespace OneHamsa.Dexterity
{

    [CustomEditor(typeof(SimpleEnumNode)), CanEditMultipleObjects]
    public class SimpleEnumNodeEditor : BaseStateNodeEditor
    {
        public override void OnInspectorGUI() {
            Legacy_OnInspectorGUI();
        }
        
        protected override void ShowFields()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(SimpleEnumNode.manualStates)));
            
            EditorGUILayout.HelpBox($"Node's state is controlled manually. " +
                                    $"Use {nameof(SimpleEnumNode.SetState)}(string) to set the state", MessageType.Info);
        }
    }
}
