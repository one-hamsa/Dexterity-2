using System.Collections.Generic;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    [CustomEditor(typeof(HierarchyNode)), CanEditMultipleObjects]
    public class HierarchyNodeEditor : BaseStateNodeEditor
    {
        private static bool s_treeFoldout = true;

        private HierarchyNode node => (HierarchyNode)target;

        private bool _livePreviewEnabled;
        private string _renderedState;
        private EditorCoroutine _transitionCoroutine;
        private readonly HashSet<IHierarchyStateProvider> _subscribed = new();

        public override void OnInspectorGUI()
        {
            Legacy_OnInspectorGUI();
        }

        protected override void ShowFields()
        {
            DrawAggregatedResult();
            DrawOpenGraphButton();
            DrawProviderTree();
            DrawLivePreviewToggle();
        }

        private void DrawOpenGraphButton()
        {
            if (targets.Length > 1) return;
            EditorGUILayout.Space(2);
            if (GUILayout.Button(new GUIContent("Open Hierarchy Graph",
                    "Open the graph window for this node — interactive overrides drive Modifiers live.")))
            {
                HierarchyGraphWindow.OpenFor(node);
            }
        }

        private void DrawAggregatedResult()
        {
            string current;
            bool isRuntime = Application.IsPlaying(this);
            if (isRuntime)
            {
                current = node.GetActiveState() != -1
                    ? Database.instance.GetStateAsString(node.GetActiveState())
                    : null;
            }
            else
            {
                current = node.EvaluateTreeEditor() ?? node.initialState;
            }

            var style = new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14
            };

            var origColor = GUI.color;
            GUI.color = string.IsNullOrEmpty(current)
                ? Color.gray
                : (isRuntime ? Color.green : Color.cyan);
            GUILayout.Label(string.IsNullOrEmpty(current) ? "(no state)" : current, style);
            GUI.color = origColor;
        }

        private void DrawProviderTree()
        {
            if (targets.Length > 1) return;

            s_treeFoldout = EditorGUILayout.Foldout(
                s_treeFoldout, "Providers", true, EditorStyles.foldoutHeader);
            if (!s_treeFoldout) return;

            EditorGUI.indentLevel++;
            DrawSubtree(node.transform);
            EditorGUI.indentLevel--;
        }

        private void DrawSubtree(Transform parent)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);

                if (child.TryGetComponent<HierarchyAggregator>(out var agg))
                {
                    DrawAggregatorRow(agg);
                    EditorGUI.indentLevel++;
                    DrawSubtree(child);
                    EditorGUI.indentLevel--;
                    continue;
                }

                // a nested node owns its own subtree
                if (child.TryGetComponent<HierarchyNode>(out _))
                    continue;

                if (child.TryGetComponent<HierarchyStateProvider>(out var leaf))
                    DrawLeafRow(leaf);

                DrawSubtree(child);
            }
        }

        private void DrawLeafRow(HierarchyStateProvider leaf)
        {
            EditorGUILayout.BeginHorizontal();

            var active = leaf.IsActive;
            var origColor = GUI.color;
            GUI.color = active ? Color.green : new Color(0.6f, 0.6f, 0.6f);
            EditorGUILayout.LabelField(
                new GUIContent($"{leaf.name} : {leaf.State}", leaf.GetType().Name),
                GUILayout.MinWidth(120));
            GUI.color = origColor;

            // Tri-state override toggle (— / ON / OFF) — same registry the graph window uses.
            var hasOverride = HierarchyPreviewOverrides.TryGet(leaf, out var ov);
            var label = !hasOverride ? "—" : ov ? "ON" : "OFF";
            if (GUILayout.Button(new GUIContent(label, "Override IsActive: — / ON / OFF"),
                    EditorStyles.miniButton, GUILayout.Width(36)))
            {
                if (!hasOverride) HierarchyPreviewOverrides.Set(leaf, true);
                else if (ov) HierarchyPreviewOverrides.Set(leaf, false);
                else HierarchyPreviewOverrides.Clear(leaf);
            }

            if (GUILayout.Button("Select", GUILayout.MaxWidth(60)))
                Selection.activeObject = leaf;

            EditorGUILayout.EndHorizontal();
        }

        private void DrawAggregatorRow(HierarchyAggregator agg)
        {
            EditorGUILayout.BeginHorizontal();

            var aggState = agg.TryGetState(out var s) ? s : null;
            var origColor = GUI.color;
            GUI.color = !string.IsNullOrEmpty(aggState)
                ? Color.yellow
                : new Color(0.6f, 0.6f, 0.6f);

            var label = $"{agg.name}  [{agg.GetType().Name}]  →  {(string.IsNullOrEmpty(aggState) ? "(idle)" : aggState)}";
            EditorGUILayout.LabelField(new GUIContent(label), GUILayout.MinWidth(180));
            GUI.color = origColor;

            if (GUILayout.Button("Select", GUILayout.MaxWidth(60)))
                Selection.activeObject = agg;

            EditorGUILayout.EndHorizontal();
        }

        private void DrawLivePreviewToggle()
        {
            if (Application.IsPlaying(this)) return;
            if (targets.Length > 1) return;

            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            var newVal = EditorGUILayout.ToggleLeft(
                new GUIContent("Live Preview",
                    "When on, edits to provider preview toggles drive Modifier transitions in edit mode."),
                _livePreviewEnabled);
            if (EditorGUI.EndChangeCheck())
            {
                _livePreviewEnabled = newVal;
                if (_livePreviewEnabled)
                {
                    SubscribeToAllProviders();
                    _renderedState = node.EvaluateTreeEditor() ?? node.initialState;
                }
                else
                {
                    UnsubscribeFromAll();
                    StopActiveTransition();
                }
            }
        }

        private void OnEnable()
        {
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            UnsubscribeFromAll();
            StopActiveTransition();
        }

        private void OnHierarchyChanged()
        {
            if (_livePreviewEnabled) SubscribeToAllProviders();
            Repaint();
        }

        private void SubscribeToAllProviders()
        {
            UnsubscribeFromAll();
            if (node == null) return;

            CollectAllProviders(node.transform, _subscribed);
            foreach (var p in _subscribed)
                p.onStateMayHaveChanged += OnProviderChanged;
        }

        private void UnsubscribeFromAll()
        {
            foreach (var p in _subscribed)
                p.onStateMayHaveChanged -= OnProviderChanged;
            _subscribed.Clear();
        }

        private static void CollectAllProviders(Transform root, HashSet<IHierarchyStateProvider> output)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (c.TryGetComponent<HierarchyNode>(out _)) continue; // nested node owns its subtree

                if (c.TryGetComponent<HierarchyAggregator>(out var agg))
                    output.Add(agg);
                if (c.TryGetComponent<HierarchyStateProvider>(out var leaf))
                    output.Add(leaf);

                CollectAllProviders(c, output);
            }
        }

        private void OnProviderChanged()
        {
            if (target == null) return;

            if (_livePreviewEnabled)
            {
                var newState = node.EvaluateTreeEditor() ?? node.initialState;
                if (newState != _renderedState)
                {
                    StartTransition(_renderedState, newState);
                    _renderedState = newState;
                }
            }

            Repaint();
        }

        private void StartTransition(string from, string to)
        {
            if (string.IsNullOrEmpty(to)) return;
            StopActiveTransition();

            var modifiers = CollectAnimatableModifiers();
            if (modifiers.Count == 0) return;

            _transitionCoroutine = EditorCoroutineUtility.StartCoroutine(
                EditorTransitions.TransitionAsync(
                    modifiers,
                    fromState: string.IsNullOrEmpty(from) ? to : from,
                    toState: to,
                    speed: 1f,
                    onEnd: () => _transitionCoroutine = null),
                this);
        }

        private void StopActiveTransition()
        {
            if (_transitionCoroutine != null)
                EditorCoroutineUtility.StopCoroutine(_transitionCoroutine);
            _transitionCoroutine = null;
        }

        private HashSet<Modifier> CollectAnimatableModifiers()
        {
            var set = new HashSet<Modifier>();
            foreach (var m in Resources.FindObjectsOfTypeAll<Modifier>())
            {
                if (m.gameObject.hideFlags != HideFlags.None) continue;
                if (m.GetNode() != node) continue;
                if (!m.animatableInEditor) continue;
                set.Add(m);
            }
            return set;
        }
    }
}
