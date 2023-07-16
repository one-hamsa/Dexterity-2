using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Reflection;
using System.Collections;
using Unity.EditorCoroutines.Editor;

namespace OneHamsa.Dexterity
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
            for (var i = 0; i < Database.instance.fieldNames.Count; ++i) {
                var field = Database.instance.fieldNames[i];
                EditorGUILayout.LabelField(field, i.ToString());
            }

            GUILayout.Label("State IDs (runtime)", EditorStyles.whiteLargeLabel);
            for (var i = 0; i < Database.instance.stateNames.Count; ++i) {
                var state = Database.instance.stateNames[i];
                EditorGUILayout.LabelField(state, i.ToString());
            }
        }
    }
}
