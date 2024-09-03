using UnityEditor;

namespace OneHamsa.Dexterity.Builtins
{
    [CustomEditor(typeof(MatrixDefinition))]
    public class MatrixDefinitionEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            if (EditorGUI.EndChangeCheck())
            {
                if (target is MatrixDefinition matrixDefinition)
                    matrixDefinition.OnInspectorChangeDetected();
            }
        }
    }
}
