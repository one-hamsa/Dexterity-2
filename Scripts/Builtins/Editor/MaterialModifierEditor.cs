using UnityEditor;
using UnityEngine;

namespace OneHamsa.Dexterity.Builtins
{
    [CustomEditor(typeof(BaseMaterialModifier), true), CanEditMultipleObjects]
    public class MaterialModifierEditor : ModifierEditor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            
            if (targets.Length > 1 || Application.IsPlaying(target))
                return;

            var matModifier = (BaseMaterialModifier) target;
            
            var origColor = GUI.contentColor;
            GUI.contentColor = matModifier.enableMaterialAnimations ? origColor : new Color(.7f, .7f, .7f);
            var icon = EditorGUIUtility.IconContent(
                matModifier.enableMaterialAnimations
                ? "ViewToolOrbit On"
                : "ViewToolOrbit");
            icon.text = matModifier.enableMaterialAnimations
                ? "  Modifier Animations Enabled"
                : "  Modifier Animations Disabled";

            if (GUILayout.Button(icon, EditorStyles.miniButton))
            {
                matModifier.enableMaterialAnimations = !matModifier.enableMaterialAnimations;
                matModifier.SetMaterialDirty();
                EditorUtility.SetDirty(matModifier);
            }
            
            GUI.contentColor = origColor;
        }
    }
}