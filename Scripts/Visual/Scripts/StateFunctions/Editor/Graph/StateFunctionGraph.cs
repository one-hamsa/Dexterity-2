using System;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// based on https://github.com/merpheus-dev/NodeBasedDialogueSystem
namespace OneHamsa.Dexterity.Visual
{
    public class StateFunctionGraph : EditorWindow
    {
        private StateFunctionGraphView _graphView;
        public StateFunction data;

        public static void CreateGraphViewWindow(StateFunction data)
        {
            var window = GetWindow<StateFunctionGraph>();
            window.data = data;
            window.titleContent = new GUIContent("State Function");
            window.Setup();

            var saveUtility = GraphSaveUtility.GetInstance(window._graphView);
            saveUtility.LoadData(data);
        }

        [OnOpenAsset(1)]
        public static bool TryOpen(int instanceID, int line)
        {
            var obj = EditorUtility.InstanceIDToObject(instanceID);
            if (obj is StateFunction)
            {
                CreateGraphViewWindow(obj as StateFunction);
                return true;
            }
            return false;
        }

        Button _saveButton, _discardButton;
        bool showInspector = true;
        private void Setup()
        {
            rootVisualElement.Clear();

            _graphView = new StateFunctionGraphView(this)
            {
                name = "State Function",
            };
            rootVisualElement.Add(_graphView);

            _saveButton = new Button(Save) { text = "Save" };
            _discardButton = new Button(Discard) { text = "Discard" };
            var toolbar = new Toolbar();
            toolbar.Add(_saveButton);
            toolbar.Add(_discardButton);
            toolbar.Add(new Button(() => showInspector = !showInspector) { text = "Toggle Inspector" });
            rootVisualElement.Add(toolbar);
            RecycleInspector();
        }

        private void OnGUI()
        {
            _graphView.ClearClassList();
            _graphView.AddToClassList(showInspector ? "inspector" : "noInspector");

            if (!showInspector)
                return;

            GUILayout.BeginArea(new Rect(new Vector2(position.width * .75f, 0), 
                new Vector2(position.width * .25f, position.height)));
            GUILayout.BeginScrollView(default);
            embeddedInspector.ShowInspectorGUI(false);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        StateFunctionEditor embeddedInspector;

        // a helper method that destroys the current inspector instance before creating a new one
        // use this in place of "Editor.CreateEditor"
        void RecycleInspector()
        {
            if (embeddedInspector != null) DestroyImmediate(embeddedInspector);
            embeddedInspector = (StateFunctionEditor)Editor.CreateEditor(data);
        }

        private void OnDisable()
        {
            rootVisualElement.Clear();
            if (embeddedInspector != null) DestroyImmediate(embeddedInspector);
        }

        private void Update()
        {
            var dirty = EditorUtility.IsDirty(data);
            titleContent.text = $"{(dirty ? "*" : "")}State Function ({data.name})";
            _saveButton.SetEnabled(dirty);
            _discardButton.SetEnabled(dirty);
        }

        void Save()
        {
            var saveUtility = GraphSaveUtility.GetInstance(_graphView);
            saveUtility.SaveData(data);
        }

        void Discard()
        {
            var saveUtility = GraphSaveUtility.GetInstance(_graphView);
            saveUtility.LoadData(data);
            EditorUtility.ClearDirty(data);
        }
    }
}