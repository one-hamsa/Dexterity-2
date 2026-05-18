using System;
using System.Collections;
using System.Collections.Generic;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// Edit-time preview driver — holistic version. Mirrors the runtime
    /// Manager loop closely instead of one-shot transitions, so:
    /// <list type="bullet">
    ///   <item><b>Per-node TransitionDelays are honored</b> — each node's
    ///         <see cref="BaseStateNode.Refresh"/> handles pendingStateChangeTime
    ///         exactly like at runtime.</item>
    ///   <item><b>Cross-node dependencies cascade naturally</b> — when an upstream
    ///         node transitions, its <c>onStateChanged</c> fires; downstream nodes'
    ///         next-frame Refresh sees the new state via <c>GetActiveState()</c>
    ///         (rather than the raw <c>EvaluateTreeEditor</c>), so they observe the
    ///         upstream's delay before applying their own.</item>
    ///   <item><b>Modifier transitions run in parallel under one Database session</b>
    ///         — no per-transition Destroy/Create races.</item>
    /// </list>
    ///
    /// <para><b>Lifecycle (two states):</b></para>
    /// <list type="bullet">
    ///   <item><b>Idle</b>: no preview targets. No Database, no coroutine, no subscriptions
    ///         on nodes.</item>
    ///   <item><b>Running</b>: Database alive (timeScale = kPreviewSpeed); each driven
    ///         node Allocate()'d (registered with Database, IDs cached); each driven
    ///         modifier Allocate()'d; per-node onStateChanged handler subscribed to
    ///         trigger modifier transitions; coroutine ticking every frame.</item>
    /// </list>
    /// <see cref="Resync"/> moves between the two whenever
    /// <see cref="DexterityPreview.AnimatableNodes"/> changes.
    ///
    /// <para><b>Frame loop per tick:</b></para>
    /// <list type="number">
    ///   <item>Mark each driven node dirty + call <see cref="BaseStateNode.Refresh"/>.
    ///         Cheap when nothing changed; applies delays + transitions when something has.
    ///         Re-evaluating every node every frame is what gives cross-node deps
    ///         their natural cascade — Node A's transition this frame becomes visible
    ///         to Node B's eval next frame.</item>
    ///   <item>For each modifier: <see cref="Modifier.ProgressTime_Editor"/> +
    ///         <see cref="Modifier.Refresh"/>. SetDirty + scene-repaint when its output
    ///         changed.</item>
    /// </list>
    ///
    /// <para><b>State-change bridge:</b> at runtime Modifier subscribes to its node's
    /// <c>onStateChanged</c> in OnEnable. We can't run OnEnable at edit time, so the
    /// driver hooks each driven node's <c>onStateChanged</c> itself and calls
    /// <see cref="Modifier.PrepareTransition_Editor"/> on the owned modifiers with
    /// (oldState, newState). The modifier then interpolates from old→new over its own
    /// configured speed.</para>
    /// </summary>
    internal static class GraphEditorPreviewDriver
    {
        private const float kPreviewSpeed = 6f;

        private static EditorCoroutine s_loop;
        private static readonly HashSet<GraphNode> s_nodes = new();
        private static readonly Dictionary<GraphNode, HashSet<Modifier>> s_mods = new();
        private static readonly Dictionary<GraphNode, Action<int, int>> s_handlers = new();

        /// <summary>True while the driver coroutine is ticking (a preview session is live).</summary>
        public static bool IsAnimating => s_loop != null;
        public static event Action onAnimatingChanged;

        [InitializeOnLoadMethod]
        private static void Init()
        {
            DexterityPreview.onChanged += Resync;
            GraphPreviewOverrides.onChanged += OnOverridesChanged;
            EditorApplication.delayCall += Resync;
            EditorSceneManager.sceneOpened += (_, __) => Resync();
            EditorSceneManager.sceneClosing += (_, __) => Stop();
            EditorApplication.playModeStateChanged += _ => Resync();
        }

        // ----- Lifecycle ------------------------------------------------------

        /// <summary>Match the running state to <see cref="DexterityPreview.AnimatableNodes"/>.</summary>
        private static void Resync()
        {
            // At runtime Manager handles all of this — we get out of the way.
            if (Application.isPlaying) { Stop(); return; }

            var desired = new HashSet<GraphNode>();
            foreach (var n in DexterityPreview.AnimatableNodes)
                if (n != null) desired.Add(n);

            if (s_nodes.SetEquals(desired)) return;

            // Set changed → restart cleanly. Stop tears everything down; Start initializes
            // the new set.
            Stop();
            if (desired.Count == 0) return;
            foreach (var n in desired) s_nodes.Add(n);
            Start();
        }

        private static void Start()
        {
            // 1) Fresh Database that lives for the entire preview session — every node
            //    + modifier below uses Database.instance during Refresh / time tracking.
            Database.Destroy();
            var db = Database.Create(DexteritySettingsProvider.settings);
            db.timeScale = kPreviewSpeed;

            // 2) Discover modifiers per driven node. Shares the FieldNode-preview
            //    discovery path (BaseStateNodeEditor.GetModifiers) so we behave the
            //    same in edge cases (prefab stages, hideFlags filter, etc.).
            s_mods.Clear();
            foreach (var n in s_nodes)
            {
                var mods = new HashSet<Modifier>();
                foreach (var m in BaseStateNodeEditor.GetModifiers(n))
                    if (m.animatableInEditor && m.gameObject.hideFlags == HideFlags.None)
                        mods.Add(m);
                s_mods[n] = mods;
            }

            // 3) Allocate everyone. Initialize registers states with Database (so
            //    GetActiveState/GetStateID work) and sets initialized = true. From now
            //    on each driven node behaves like a runtime-initialized node.
            foreach (var n in s_nodes) n.Allocate();
            foreach (var kv in s_mods) foreach (var m in kv.Value) m.Allocate();

            // 4) Snap each node to its current state without animating, then park its
            //    modifiers at that state so they don't run a "from default" animation
            //    when the preview window first opens.
            foreach (var n in s_nodes)
            {
                if (!n.isActiveAndEnabled) continue;
                n.UpdateState(ignoreDelays: true);
                var stateName = Database.instance.GetStateAsString(n.GetActiveState());
                if (s_mods.TryGetValue(n, out var mods))
                    foreach (var m in mods)
                        m.PrepareTransition_Editor(stateName, stateName);
            }

            // 5) Bridge: subscribe a per-node handler to onStateChanged that re-primes
            //    the node's modifiers with the new (old → new) target whenever the
            //    node transitions during the frame loop.
            s_handlers.Clear();
            foreach (var n in s_nodes)
            {
                var captured = n;
                Action<int, int> handler = (oldS, newS) => OnNodeStateChanged(captured, oldS, newS);
                n.onStateChanged += handler;
                s_handlers[n] = handler;
            }

            // 6) Start the tick.
            s_loop = EditorCoroutineUtility.StartCoroutineOwnerless(FrameLoop());
            onAnimatingChanged?.Invoke();
        }

        private static IEnumerator FrameLoop()
        {
            while (s_nodes.Count > 0 && Database.instance != null)
            {
                yield return null;

                // Tick nodes. MarkStateDirty every frame so each node always re-evaluates
                // — this is what gives cross-node deps their cascade: Node B reading
                // Node A.GetActiveState() will see A's just-applied transition next frame.
                // (Refresh internally short-circuits when no actual change happened, so
                // this is cheap.)
                foreach (var n in s_nodes)
                {
                    if (n == null || !n.isActiveAndEnabled) continue;
                    n.MarkStateDirty();
                    n.Refresh();
                }

                // Tick modifiers. ProgressTime advances each one's edit-time clock; Refresh
                // re-runs the transition strategy with that clock; IsChanged decides whether
                // to SetDirty + RepaintAll.
                bool anyChanged = false;
                foreach (var kv in s_mods)
                {
                    foreach (var m in kv.Value)
                    {
                        if (m == null) continue;
                        m.ProgressTime_Editor(Database.instance.deltaTime);
                        try
                        {
                            m.Refresh();
                            var changed = m.IsChanged();
                            anyChanged |= changed;
                            if (changed) EditorUtility.SetDirty(m);
                        }
                        catch (Exception e) { Debug.LogException(e); }
                    }
                }
                if (anyChanged) SceneView.RepaintAll();
            }

            // Loop exited (Database destroyed or s_nodes cleared externally — usually
            // via Stop() which also cancels this coroutine).
            s_loop = null;
            onAnimatingChanged?.Invoke();
        }

        /// <summary>Node finished transitioning to a new state → re-prime its modifiers.</summary>
        private static void OnNodeStateChanged(GraphNode node, int oldStateId, int newStateId)
        {
            if (Database.instance == null) return;
            if (!s_mods.TryGetValue(node, out var mods)) return;

            var oldName = Database.instance.GetStateAsString(oldStateId);
            var newName = Database.instance.GetStateAsString(newStateId);
            foreach (var m in mods)
                if (m != null) m.PrepareTransition_Editor(oldName, newName);
        }

        private static void OnOverridesChanged()
        {
            // Override flipped → every driven node should re-evaluate on its next
            // Refresh. The frame loop already marks them dirty too, but doing it here
            // synchronously means the response feels immediate.
            if (s_loop == null) return;
            foreach (var n in s_nodes)
                if (n != null && n.isActiveAndEnabled) n.MarkStateDirty();
        }

        private static void Stop()
        {
            if (s_loop == null && s_nodes.Count == 0) return;
            var wasRunning = s_loop != null;

            if (s_loop != null) EditorCoroutineUtility.StopCoroutine(s_loop);
            s_loop = null;

            foreach (var kv in s_handlers)
                if (kv.Key != null) kv.Key.onStateChanged -= kv.Value;
            s_handlers.Clear();

            // Wipe edit-time state on every driven node before destroying Database.
            // Without this, `initialized` stays true and downstream NodeStateProviders
            // read a frozen GetActiveState() — the dependent stays locked on the last
            // preview state long after the session ended.
            foreach (var n in s_nodes)
                if (n != null) n.ResetEditorState();

            s_mods.Clear();
            s_nodes.Clear();

            Database.Destroy();
            if (wasRunning) onAnimatingChanged?.Invoke();
        }
    }
}
