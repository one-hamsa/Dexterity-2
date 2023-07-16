using UnityEditor;

namespace OneHamsa.Dexterity
{

    [CustomEditor(typeof(StateProxyNode)), CanEditMultipleObjects]
    public class StateProxyNodeEditor : BaseStateNodeEditor
    {
        public override void OnInspectorGUI() {
            Legacy_OnInspectorGUI();
        }
        
        protected override void ShowFields()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(StateProxyNode.stateProxies)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(StateProxyNode.defaultStateName)));
        }
    }
}
