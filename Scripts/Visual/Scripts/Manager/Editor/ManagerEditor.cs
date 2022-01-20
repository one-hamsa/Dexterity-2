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

            var manager = target as Manager;

            GUILayout.Label("Field IDs (runtime)", EditorStyles.whiteLargeLabel);
            for (var i = 0; i < manager.fieldNames.Length; ++i) {
                var field = manager.fieldNames[i];
                EditorGUILayout.LabelField(field, i.ToString());
            }

            GUILayout.Label("State IDs (runtime)", EditorStyles.whiteLargeLabel);
            for (var i = 0; i < manager.stateNames.Count; ++i) {
                var state = manager.stateNames[i];
                EditorGUILayout.LabelField(state, i.ToString());
            }
        }
    }
}
