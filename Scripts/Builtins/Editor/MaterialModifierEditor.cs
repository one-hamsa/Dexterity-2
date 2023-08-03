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
            
            if (Application.IsPlaying(target))
                return;

            var matModifier = (BaseMaterialModifier) target;
            
            var origColor = GUI.contentColor;
            GUI.contentColor = matModifier.enableEditorMaterialAnimations ? origColor : new Color(.7f, .7f, .7f);
            var icon = EditorGUIUtility.IconContent(
                matModifier.enableEditorMaterialAnimations
                ? "ViewToolOrbit On"
                : "ViewToolOrbit");
            icon.text = matModifier.enableEditorMaterialAnimations
                ? "  Modifier Animations Enabled"
                : "  Modifier Animations Disabled";

            if (GUILayout.Button(icon, EditorStyles.miniButton))
            {
                var newValue = !matModifier.enableEditorMaterialAnimations;
                foreach (var obj in targets)
                {
                    var matMod = (BaseMaterialModifier) obj;
                    matMod.enableEditorMaterialAnimations = newValue;
                    matMod.SetMaterialDirty();
                    EditorUtility.SetDirty(modifier);
                }
            }
            
            GUI.contentColor = origColor;
        }
    }
}