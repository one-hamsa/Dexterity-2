using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// based on https://github.com/merpheus-dev/NodeBasedDialogueSystem
namespace OneHamsa.Dexterity.Visual
{
    public class StateFunctionGraph : EditorWindow
    {
        private string _fileName = "New State Function";

        private StateFunctionGraphView _graphView;
        private StateFunction _sfContainer;
        private TextField _fileNameTextField;

        public event Action OnRefresh;

        public static void CreateGraphViewWindow(StateFunction data = null)
        {
            var window = GetWindow<StateFunctionGraph>();
            window.titleContent = new GUIContent("State Function Graph");
            if (data != null)
            {
                window._fileNameTextField.SetValueWithoutNotify(data.name);
                var saveUtility = GraphSaveUtility.GetInstance(window._graphView);
                saveUtility.LoadData(data);
            }
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

        private void ConstructGraphView()
        {
            _graphView = new StateFunctionGraphView(this)
            {
                name = "State Function Graph",
            };
            _graphView.StretchToParentSize();
            rootVisualElement.Add(_graphView);
        }

        private void GenerateToolbar()
        {
            var toolbar = new Toolbar();

            _fileNameTextField = new TextField("File Name:");
            _fileNameTextField.SetValueWithoutNotify(_fileName);
            _fileNameTextField.MarkDirtyRepaint();
            _fileNameTextField.RegisterValueChangedCallback(evt => _fileName = evt.newValue);
            toolbar.Add(_fileNameTextField);
            toolbar.Add(new Button(() => RequestDataOperation(true)) {text = "Save"});
            toolbar.Add(new Button(() => RequestDataOperation(false)) { text = "Load" });
            toolbar.Add(new Button(() => OnRefresh?.Invoke()) { text = "Refresh" });
            rootVisualElement.Add(toolbar);
        }

        private void RequestDataOperation(bool save)
        {
            if (!string.IsNullOrEmpty(_fileName))
            {
                var saveUtility = GraphSaveUtility.GetInstance(_graphView);
                if (save)
                    saveUtility.SaveGraph(_fileName);
                else
                    saveUtility.LoadData(_fileName);
            }
            else
            {
                EditorUtility.DisplayDialog("Invalid File name", "Please Enter a valid filename", "OK");
            }
        }

        private void OnEnable()
        {
            ConstructGraphView();
            GenerateToolbar();
            //GenerateMiniMap();
            //GenerateBlackBoard();
        }

        private void GenerateMiniMap()
        {
            var miniMap = new MiniMap {anchored = true};
            var cords = _graphView.contentViewContainer.WorldToLocal(new Vector2(this.maxSize.x - 10, 30));
            miniMap.SetPosition(new Rect(cords.x, cords.y, 200, 140));
            _graphView.Add(miniMap);
        }

        private void GenerateBlackBoard()
        {
            var blackboard = new Blackboard(_graphView);
            blackboard.Add(new BlackboardSection {title = "Exposed Variables"});
            blackboard.addItemRequested = _blackboard =>
            {
                _graphView.AddPropertyToBlackBoard(ExposedProperty.CreateInstance(), false);
            };
            blackboard.editTextRequested = (_blackboard, element, newValue) =>
            {
                var oldPropertyName = ((BlackboardField) element).text;
                if (_graphView.ExposedProperties.Any(x => x.PropertyName == newValue))
                {
                    EditorUtility.DisplayDialog("Error", "This property name already exists, please chose another one.",
                        "OK");
                    return;
                }

                var targetIndex = _graphView.ExposedProperties.FindIndex(x => x.PropertyName == oldPropertyName);
                _graphView.ExposedProperties[targetIndex].PropertyName = newValue;
                ((BlackboardField) element).text = newValue;
            };
            blackboard.SetPosition(new Rect(10,30,200,300));
            _graphView.Add(blackboard);
            _graphView.Blackboard = blackboard;
        }

        private void OnDisable()
        {
            rootVisualElement.Remove(_graphView);
        }
    }
}