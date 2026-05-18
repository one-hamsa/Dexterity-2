using System.Collections.Generic;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    [CustomEditor(typeof(HierarchyNode)), CanEditMultipleObjects]
    public class HierarchyNodeEditor : BaseStateNodeEditor
    {
        private static bool s_sourcesFoldout = true;

        private HierarchyNode node => (HierarchyNode)target;

        private bool _livePreviewEnabled;
        private string _renderedState;
        private EditorCoroutine _transitionCoroutine;
        private readonly HashSet<IDexteritySource> _subscribed = new();

        public override void OnInspectorGUI()
        {
            Legacy_OnInspectorGUI();
        }

        protected override void ShowFields()
        {
            DrawAggregatedResult();
            DrawOpenGraphButton();
            DrawSources();
            DrawLivePreviewToggle();
        }

        private void DrawOpenGraphButton()
        {
            if (targets.Length > 1) return;
            EditorGUILayout.Space(2);
            if (GUILayout.Button(new GUIContent("Open Hierarchy Graph",
                    "Open the graph window for this node — interactive overrides drive Modifiers live.")))
            {
                DexterityGraphWindow.OpenFor(node);
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

        private void DrawSources()
        {
            if (targets.Length > 1) return;

            s_sourcesFoldout = EditorGUILayout.Foldout(
                s_sourcesFoldout, "Sources on host", true, EditorStyles.foldoutHeader);
            if (!s_sourcesFoldout) return;

            EditorGUI.indentLevel++;
            foreach (var p in node.GetComponents<HierarchyStateProvider>())
                DrawSourceRow(p, isAggregator: false);
            foreach (var a in node.GetComponents<HierarchyAggregator>())
                DrawSourceRow(a, isAggregator: true);
            EditorGUI.indentLevel--;
        }

        private void DrawSourceRow(Component src, bool isAggregator)
        {
            var srcAsSource = (IDexteritySource)src;

            EditorGUILayout.BeginHorizontal();

            var active = srcAsSource.IsActive;
            var origColor = GUI.color;
            GUI.color = active
                ? (isAggregator ? Color.yellow : Color.green)
                : new Color(0.6f, 0.6f, 0.6f);

            var typeLabel = DexterityGraphView.StripSuffix(src.GetType().Name,
                isAggregator ? "Aggregator" : "Provider");
            EditorGUILayout.LabelField(
                new GUIContent(typeLabel, isAggregator ? "Aggregator on host" : "Provider on host"),
                GUILayout.MinWidth(180));
            GUI.color = origColor;

            // Tri-state override toggle (— / ON / OFF).
            var hasOverride = HierarchyPreviewOverrides.TryGet(srcAsSource, out var ov);
            var label = !hasOverride ? "—" : ov ? "ON" : "OFF";
            if (GUILayout.Button(new GUIContent(label, "Override IsActive: — / ON / OFF"),
                    EditorStyles.miniButton, GUILayout.Width(36)))
            {
                if (!hasOverride) HierarchyPreviewOverrides.Set(srcAsSource, true);
                else if (ov) HierarchyPreviewOverrides.Set(srcAsSource, false);
                else HierarchyPreviewOverrides.Clear(srcAsSource);
            }

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
                    SubscribeToAllSources();
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
            if (_livePreviewEnabled) SubscribeToAllSources();
            Repaint();
        }

        private void SubscribeToAllSources()
        {
            UnsubscribeFromAll();
            if (node == null) return;

            foreach (var p in node.GetComponents<HierarchyStateProvider>())
                _subscribed.Add(p);
            foreach (var a in node.GetComponents<HierarchyAggregator>())
                _subscribed.Add(a);

            foreach (var s in _subscribed)
                s.onStateMayHaveChanged += OnSourceChanged;
        }

        private void UnsubscribeFromAll()
        {
            foreach (var s in _subscribed)
                if (s != null) s.onStateMayHaveChanged -= OnSourceChanged;
            _subscribed.Clear();
        }

        private void OnSourceChanged()
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
