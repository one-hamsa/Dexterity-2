using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System;

namespace OneHamsa.Dexterity
{
    public class NodeReferenceEditorWindow : EditorWindow
    {
        NodeReference reference;

        public static NodeReferenceEditorWindow Open(NodeReference reference)
        {
            var win = GetWindow<NodeReferenceEditorWindow>(reference.name);
            win.autoRepaintOnSceneChange = true;
            win.reference = reference;
            return win;
        }

        Vector2 scrollPos;
        private void OnGUI()
        {
            if (reference == null)
            {
                Close();
                return;
            }

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            var editor = Editor.CreateEditor(reference, typeof(NodeReferenceEditor));
            editor.OnInspectorGUI();
            EditorGUILayout.EndScrollView();
        }
    }
}