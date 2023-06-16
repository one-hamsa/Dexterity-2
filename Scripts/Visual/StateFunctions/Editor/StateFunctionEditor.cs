using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System;
using Unity.EditorCoroutines.Editor;
using UnityEditor.SceneManagement;
using UnityEditor.IMGUI.Controls;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace OneHamsa.Dexterity
{
    [CustomEditor(typeof(StateFunction))]
    public class StateFunctionEditor : Editor
    {
        private StepListView listView;

        public override VisualElement CreateInspectorGUI()
        {
            listView = new StepListView(serializedObject, nameof(StateFunction.steps));
            return listView;
        }
    }
}
