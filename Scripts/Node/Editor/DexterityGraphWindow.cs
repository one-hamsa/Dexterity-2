using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// Editable graph window for a <see cref="HierarchyNode"/>. Builds GraphView nodes
    /// for the Out node and every provider/aggregator on the host GameObject; draws
    /// edges from each source's <see cref="DexterityEdge"/> outputs list; lets users
    /// drag-to-connect, drag-to-reposition, and add/delete sources via the context menu.
    ///
    /// All writes route through <c>SerializedObject</c> + <c>ApplyModifiedProperties</c>
    /// so Unity's prefab-override tracking sees them (spike rule 2). Positions persist
    /// via the <c>graphPosition</c> field on each component.
    /// </summary>
    public class DexterityGraphWindow : EditorWindow
    {
        [MenuItem("Tools/Dexterity/Hierarchy Graph")]
        public static void Open()
        {
            var w = GetWindow<DexterityGraphWindow>(false, "Dexterity Graph", true);
            w.minSize = new Vector2(480, 320);
        }

        public static void OpenFor(HierarchyNode node)
        {
            Open();
            var w = GetWindow<DexterityGraphWindow>();
            w._lockedNode = node;
            w.RebuildFromSelection();
        }

        private DexterityGraphView _view;
        private HierarchyNode _lockedNode;       // when non-null, ignore selection changes
        private Toggle _lockToggle;
        private Label _headerLabel;

        private void OnEnable()
        {
            rootVisualElement.Clear();
            var toolbar = new Toolbar();
            _headerLabel = new Label("(no node selected)") { style = { unityFontStyleAndWeight = FontStyle.Bold } };
            toolbar.Add(_headerLabel);
            toolbar.Add(new ToolbarSpacer { flex = true });
            _lockToggle = new Toggle("Lock") { value = false };
            _lockToggle.RegisterValueChangedCallback(evt =>
            {
                _lockedNode = evt.newValue ? CurrentTargetNode() : null;
                RebuildFromSelection();
            });
            toolbar.Add(_lockToggle);
            rootVisualElement.Add(toolbar);

            _view = new DexterityGraphView { style = { flexGrow = 1 } };
            rootVisualElement.Add(_view);

            Selection.selectionChanged += RebuildFromSelection;
            Undo.undoRedoPerformed += RebuildFromSelection;
            EditorApplication.hierarchyChanged += RebuildFromSelection;

            RebuildFromSelection();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= RebuildFromSelection;
            Undo.undoRedoPerformed -= RebuildFromSelection;
            EditorApplication.hierarchyChanged -= RebuildFromSelection;
        }

        private HierarchyNode CurrentTargetNode()
        {
            if (_lockedNode != null) return _lockedNode;
            var go = Selection.activeGameObject;
            return go != null ? go.GetComponent<HierarchyNode>() : null;
        }

        private void RebuildFromSelection()
        {
            var target = CurrentTargetNode();
            if (_headerLabel != null)
                _headerLabel.text = target != null ? $"{target.gameObject.name} — HierarchyNode" : "(no node selected)";
            _view?.RebuildGraph(target);
        }
    }
}
