using UnityEngine;
using UnityEditor;
using GraphProcessor;

namespace OneHamsa.Dexterity.Visual
{
    public class StateFunctionGraphToolbarView : ToolbarView
    {
        bool dirty;
        public StateFunctionGraphToolbarView(BaseGraphView graphView) : base(graphView)
        {
            graphView.initialized += GraphView_initialized;
        }

        private void HandleGraphChanges(GraphChanges changes)
        {
            dirty = true;
            RefreshSaveButtonColor();
        }

        private void RefreshSaveButtonColor()
        {
            SetButtonColor("Save", dirty ? Color.green : Color.gray);
        }

        protected override void AddButtons()
        {
            AddButton("Center", graphView.ResetPositionAndZoom);
            AddButton("Save", SaveChanges, left: false);
            RefreshSaveButtonColor();
        }

        private void GraphView_initialized()
        {
            graphView.onAfterGraphChanged += HandleGraphChanges;
        }

        private void SaveChanges()
        {
            graphView.SaveGraphToDisk();
            // also save other assets (events etc.) that may have changed
            AssetDatabase.SaveAssets();
            dirty = false;
            RefreshSaveButtonColor();
        }
    }
}