using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace OneHamsa.Dexterity.Visual
{
    public class NodeSearchWindow : ScriptableObject,ISearchWindowProvider
    {
        private EditorWindow _window;
        private StateFunctionGraphView _graphView;

        private Texture2D _indentationIcon;
        
        public void Configure(EditorWindow window,StateFunctionGraphView graphView)
        {
            _window = window;
            _graphView = graphView;
            
            //Transparent 1px indentation icon as a hack
            _indentationIcon = new Texture2D(1,1);
            _indentationIcon.SetPixel(0,0,new Color(0,0,0,0));
            _indentationIcon.Apply();
        }
        
        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var tree = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Create Node"), 0),
                new SearchTreeEntry(new GUIContent("Condition", _indentationIcon))
                {
                    level = 1, userData = new ConditionNode()
                },
                new SearchTreeEntry(new GUIContent("Decision", _indentationIcon))
                {
                    level = 1, userData = new DecisionNode()
                },
                new SearchTreeGroupEntry(new GUIContent("Misc."), 1),
            };

            return tree;
        }

        public bool OnSelectEntry(SearchTreeEntry SearchTreeEntry, SearchWindowContext context)
        {
            //Editor window-based mouse position
            var mousePosition = _window.rootVisualElement.ChangeCoordinatesTo(_window.rootVisualElement.parent,
                context.screenMousePosition - _window.position.position);
            var graphMousePosition = _graphView.contentViewContainer.WorldToLocal(mousePosition);
            switch (SearchTreeEntry.userData)
            {
                case ConditionNode cNode:
                    _graphView.CreateNewConditionNode(graphMousePosition);
                    return true;
                case DecisionNode dNode:
                    _graphView.CreateNewDecisionNode(graphMousePosition);
                    return true;
            }
            return false;
        }
    }
}