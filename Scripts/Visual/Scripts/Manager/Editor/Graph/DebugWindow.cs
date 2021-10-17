using System;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace OneHamsa.Dexterity.Visual
{
    public class DebugWindow : EditorWindow
    {
        private DebugGraphView _graphView;

        [MenuItem("Dexterity/Debug")]
        public static void CreateDebugWindow()
        {
            var window = GetWindow<DebugWindow>();
            window.titleContent = new GUIContent("Dexterity Debug");
        }


        private void ConstructGraphView()
        {
            _graphView = new DebugGraphView(this)
            {
                name = "Debug",
            };
            _graphView.StretchToParentSize();
            rootVisualElement.Add(_graphView);
        }

        private void OnEnable()
        {
            ConstructGraphView();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                var label = new Label("Enter Play Mode to watch debug graph");
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                label.style.flexGrow = 1;
                label.style.fontSize = 25;

                rootVisualElement.Clear();
                rootVisualElement.Add(label);
                return;
            }

            _graphView.Update();
        }
    }
}