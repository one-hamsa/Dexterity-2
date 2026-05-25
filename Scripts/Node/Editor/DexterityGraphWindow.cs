using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// Editable graph window for a <see cref="GraphNode"/>. Builds GraphView nodes
    /// for the Out node and every provider/aggregator on the host GameObject; draws
    /// edges from each source's <see cref="DexterityEdge"/> outputs list; lets users
    /// drag-to-connect, drag-to-reposition, and add/delete sources via the context menu.
    ///
    /// All writes route through <c>SerializedObject</c> + <c>ApplyModifiedProperties</c>
    /// so Unity's prefab-override tracking sees them (spike rule 2). Positions persist
    /// via the <c>graphPosition</c> field on each component.
    ///
    /// <b>Preview model.</b> The Preview toggle (default on) opts this window's target
    /// into the global preview set. The driver then animates only that node + its
    /// upstream dependencies — opt-in per node, so big scenes don't pay for every
    /// GraphNode every frame.
    /// </summary>
    public class DexterityGraphWindow : EditorWindow
    {
        /// <summary>
        /// Fired by <see cref="GraphNodeEditor"/> when the user edits the stateInputs
        /// list in the inspector. Every open graph window subscribes so its ports + edges
        /// re-render against the new list without a manual selection change.
        /// </summary>
        internal static event System.Action onStateInputsEditedInInspector;
        internal static void NotifyStateInputsEdited() => onStateInputsEditedInInspector?.Invoke();

        [MenuItem("Tools/Dexterity/Graph")]
        public static void Open()
        {
            // Generic menu entry — single shared window that follows Selection.
            var w = GetWindow<DexterityGraphWindow>(false, "Dexterity Graph", true);
            w.minSize = new Vector2(480, 320);
        }

        /// <summary>
        /// Opens a fresh graph window pinned to the given node. Each call creates a
        /// new independent window — the user can have several open at once, one per
        /// node they're inspecting. The generic <see cref="Open"/> menu entry still
        /// gives a single shared window that follows Selection.
        /// </summary>
        public static void OpenFor(GraphNode node)
        {
            var w = CreateWindow<DexterityGraphWindow>();
            w.minSize = new Vector2(480, 320);
            w.titleContent = new GUIContent(node != null
                ? $"Graph: {node.name}"
                : "Dexterity Graph");
            // Pin to this node so it doesn't drift on Selection changes.
            w._pinnedNode = node;
            if (w._pinToggle != null) w._pinToggle.SetValueWithoutNotify(true);
            w.RebuildFromSelection();
        }

        private DexterityGraphView _view;
        private GraphNode _pinnedNode;       // when non-null, ignore selection changes
        private GraphNode _registeredTarget; // last node we added to the preview set
        private Toggle _pinToggle;
        private Toggle _previewToggle;
        private Label _headerLabel;
        private Label _modePill;

        // Track what we last rebuilt against. EditorApplication.hierarchyChanged fires
        // for any scene-graph change (incl. Modifier transitions touching scene objects
        // during preview), so we'd previously tear down + rebuild the whole graph on
        // every override toggle. These let us skip the full rebuild when nothing
        // structurally relevant changed.
        private GraphNode _lastRenderedTarget;
        private int _lastSourceCount;
        private System.Collections.Generic.HashSet<Component> _lastSourceSet = new();

        private void OnEnable()
        {
            rootVisualElement.Clear();
            var toolbar = new Toolbar();

            // Mode pill on the left — None / Preview / Live (reflects current target).
            _modePill = new Label();
            _modePill.style.unityFontStyleAndWeight = FontStyle.Bold;
            _modePill.style.paddingLeft = 8f;
            _modePill.style.paddingRight = 8f;
            _modePill.style.marginRight = 8f;
            _modePill.style.color = Color.white;
            toolbar.Add(_modePill);

            _headerLabel = new Label("(no node selected)") { style = { unityFontStyleAndWeight = FontStyle.Bold } };
            toolbar.Add(_headerLabel);
            toolbar.Add(new ToolbarSpacer { flex = true });

            _previewToggle = new Toggle("Preview") { value = true };
            _previewToggle.tooltip = "Auto-preview this node + upstream dependencies. " +
                                     "Turn off to edit graph structure without driving Modifier transitions.";
            _previewToggle.RegisterValueChangedCallback(_ => SyncPreviewRegistration());
            toolbar.Add(_previewToggle);

            _pinToggle = new Toggle("Pin") { value = false };
            _pinToggle.tooltip = "Pin the graph to the currently displayed node. While pinned, " +
                                 "selecting other GameObjects won't switch the graph view.";
            _pinToggle.RegisterValueChangedCallback(evt =>
            {
                _pinnedNode = evt.newValue ? CurrentTargetNode() : null;
                RebuildFromSelection();
            });
            toolbar.Add(_pinToggle);
            rootVisualElement.Add(toolbar);

            _view = new DexterityGraphView { style = { flexGrow = 1 } };
            rootVisualElement.Add(_view);

            Selection.selectionChanged += RebuildFromSelection;
            Undo.undoRedoPerformed += RebuildFromSelection;
            EditorApplication.hierarchyChanged += RebuildFromSelection;
            DexterityPreview.onChanged += RefreshModePill;
            onStateInputsEditedInInspector += RebuildFromSelection;

            RefreshModePill();
            RebuildFromSelection();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= RebuildFromSelection;
            Undo.undoRedoPerformed -= RebuildFromSelection;
            EditorApplication.hierarchyChanged -= RebuildFromSelection;
            DexterityPreview.onChanged -= RefreshModePill;
            onStateInputsEditedInInspector -= RebuildFromSelection;

            // Drop our preview-set membership cleanly.
            if (_registeredTarget != null)
            {
                DexterityPreview.RemoveTarget(_registeredTarget);
                _registeredTarget = null;
            }
        }

        private void RefreshModePill()
        {
            if (_modePill == null) return;
            var target = CurrentTargetNode();
            _modePill.text = DexterityPreview.GetNodeLabel(target);
            _modePill.style.backgroundColor = DexterityPreview.GetNodeColor(target);
            _view?.RefreshActiveHighlight();
        }

        /// <summary>
        /// Idempotent: registers <see cref="CurrentTargetNode"/> with the preview set
        /// if the toggle is on, and unregisters anything we previously registered.
        /// Called whenever the target or toggle changes.
        /// </summary>
        private void SyncPreviewRegistration()
        {
            var want = (_previewToggle != null && _previewToggle.value) ? CurrentTargetNode() : null;
            if (_registeredTarget == want) { RefreshModePill(); return; }

            if (_registeredTarget != null)
                DexterityPreview.RemoveTarget(_registeredTarget);
            _registeredTarget = want;
            if (_registeredTarget != null)
                DexterityPreview.AddTarget(_registeredTarget);

            RefreshModePill();
        }

        private GraphNode CurrentTargetNode()
        {
            if (_pinnedNode != null) return _pinnedNode;
            var go = Selection.activeGameObject;
            return go != null ? go.GetComponent<GraphNode>() : null;
        }

        private void RebuildFromSelection()
        {
            var target = CurrentTargetNode();
            if (_headerLabel != null)
                _headerLabel.text = target != null ? $"{target.gameObject.name} — GraphNode" : "(no node selected)";
            SyncPreviewRegistration();

            // Decide between full rebuild vs cheap refresh.
            //   Full rebuild: target changed OR the host's source-component set changed
            //   (different count, or any source was added/removed).
            //   Cheap refresh: just repaint active-port highlight + mode pill — covers
            //   override toggles, modifier transitions, and other non-structural noise.
            bool needsFullRebuild = target != _lastRenderedTarget
                                    || HostSourceSetChanged(target);
            if (needsFullRebuild)
            {
                _lastRenderedTarget = target;
                CaptureSourceSet(target);
                _view?.RebuildGraph(target);
            }
            else
            {
                _view?.RefreshActiveHighlight();
            }
        }

        private bool HostSourceSetChanged(GraphNode target)
        {
            if (target == null) return _lastSourceCount != 0 || _lastSourceSet.Count != 0;
            var providers = target.GetComponents<GraphStateProvider>();
            var aggregators = target.GetComponents<GraphAggregator>();
            int count = providers.Length + aggregators.Length;
            if (count != _lastSourceCount) return true;
            foreach (var p in providers) if (!_lastSourceSet.Contains(p)) return true;
            foreach (var a in aggregators) if (!_lastSourceSet.Contains(a)) return true;
            return false;
        }

        private void CaptureSourceSet(GraphNode target)
        {
            _lastSourceSet.Clear();
            if (target == null) { _lastSourceCount = 0; return; }
            foreach (var p in target.GetComponents<GraphStateProvider>()) _lastSourceSet.Add(p);
            foreach (var a in target.GetComponents<GraphAggregator>()) _lastSourceSet.Add(a);
            _lastSourceCount = _lastSourceSet.Count;
        }
    }
}
