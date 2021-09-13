using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System;

namespace OneHamsa.Dexterity.Visual
{
    public class NodeReferenceEditorWindow : EditorWindow
    {
        NodeReference reference;

        public static NodeReferenceEditorWindow Open(NodeReference reference)
        {
            var win = GetWindow<NodeReferenceEditorWindow>(reference.name);
            win.reference = reference;
            return win;
        }
        
        private void OnGUI()
        {
            if (reference == null)
                return;

            var editor = Editor.CreateEditor(reference, typeof(NodeReferenceEditor));
            editor.OnInspectorGUI();
        }
    }
}