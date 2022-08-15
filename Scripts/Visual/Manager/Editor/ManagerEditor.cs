using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Reflection;
using System.Collections;
using Unity.EditorCoroutines.Editor;

namespace OneHamsa.Dexterity.Visual
{
    [CustomEditor(typeof(Manager), true)]
    public class ManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (!Application.isPlaying)
                return;

            GUILayout.Label("Field IDs (runtime)", EditorStyles.whiteLargeLabel);
            for (var i = 0; i < Core.instance.fieldNames.Count; ++i) {
                var field = Core.instance.fieldNames[i];
                EditorGUILayout.LabelField(field, i.ToString());
            }

            GUILayout.Label("State IDs (runtime)", EditorStyles.whiteLargeLabel);
            for (var i = 0; i < Core.instance.stateNames.Count; ++i) {
                var state = Core.instance.stateNames[i];
                EditorGUILayout.LabelField(state, i.ToString());
            }
        }
    }
}
