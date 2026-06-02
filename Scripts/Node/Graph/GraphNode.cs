using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// State node whose state is decided by an explicit list of named ports (the "Out
    /// node" of a Dexterity graph). Sources and aggregators living as components on the
    /// SAME GameObject feed bool signals into named ports via their
    /// <see cref="DexterityEdge"/> output list.
    ///
    /// Ports come in two kinds:
    /// <list type="bullet">
    ///   <item><b><see cref="states"/></b> — priority-ordered. Evaluation iterates them in
    ///         order; the first with any active source feeding it wins and becomes the
    ///         active state. They appear in <see cref="GetStateNames"/> so modifiers sync
    ///         rows for them. If none is active, falls back to
    ///         <see cref="BaseStateNode.initialState"/>.</item>
    ///   <item><b><see cref="inputs"/></b> — raw signal ports. Wire-able and readable via
    ///         <see cref="GetRawInput(string)"/>, but they never become the active state and
    ///         stay invisible to modifiers. The canonical use is a click listener reading
    ///         raw Pressed/Hover signals on a toggle button whose real states are the
    ///         mode-gated combinations (Shelf/ShelfHover/ShelfPress + Drop/...).</item>
    /// </list>
    ///
    /// Operators are computed in topological order (their inputs are resolved
    /// before they are). Cycles fall back to <see cref="BaseStateNode.initialState"/>
    /// with an error log.
    /// </summary>
    [AddComponentMenu("Dexterity/Graph Node")]
    [DefaultExecutionOrder(Manager.nodeExecutionPriority)]
    public class GraphNode : BaseStateNode
    {
        [SerializeField, Tooltip("Priority-ordered states (high → low). Port name = state name. " +
            "First port with any active source wins; falls back to initialState when none is active.")]
        private List<string> states = new();

        [SerializeField, Tooltip("Raw input ports: wire-able and readable via GetRawInput(), but they " +
            "never become the active state and stay invisible to modifiers. E.g. the Pressed/Hover " +
            "signals a listener reads on a toggle whose real states are the mode-gated combinations.")]
        private List<string> inputs = new();

        [SerializeField, HideInInspector]
        private Vector2 graphPosition;

        // -- Source management (sources self-attach on enable) -----------------
        private readonly HashSet<IDexteritySource> _attached = new();

        /// <summary>
        /// Fires whenever any attached source's value may have changed (and when
        /// sources attach/detach). Listeners that want to react to <b>raw input</b>
        /// signals (e.g. <see cref="Builtins.GraphNodeClickListener"/> polling
        /// <see cref="GetRawInput(string)"/>) subscribe here — <c>onStateChanged</c>
        /// only fires on priority-resolved state transitions, which masks press-
        /// under-disabled and similar cases the raw listener cares about.
        /// </summary>
        public event System.Action onInputsMayHaveChanged;

        internal void AttachSource(IDexteritySource source)
        {
            if (_attached.Add(source))
            {
                source.onStateMayHaveChanged += OnSourceChanged;
                _topoDirty = true;
                OnSourceChanged();
            }
        }

        internal void DetachSource(IDexteritySource source)
        {
            if (_attached.Remove(source))
            {
                source.onStateMayHaveChanged -= OnSourceChanged;
                _topoDirty = true;
                OnSourceChanged();
            }
        }

        private void OnSourceChanged()
        {
            MarkStateDirty();
            onInputsMayHaveChanged?.Invoke();
        }

        // MarkStateDirty inherited as public from BaseStateNode.

        // -- Evaluation caches --------------------------------------------------
        private bool _topoDirty = true;
        private bool _topoCycleDetected;
        private readonly List<IDexteritySource> _allSources = new();
        private readonly List<IDexteritySource> _topoOrdered = new();
        private readonly HashSet<IDexteritySource> _visited = new();
        private readonly HashSet<IDexteritySource> _inProgress = new();
        private readonly Dictionary<string, List<IDexteritySource>> _sourcesByPort = new();
        private readonly Dictionary<IDexteritySource, bool> _activeCache = new();
        private readonly List<bool> _aggInputScratch = new();

        // Runtime-only: port names resolved to Database int IDs, parallel to the
        // states / inputs lists respectively. Built lazily on first runtime access
        // (Database may not be alive at edit time).
        [System.NonSerialized] private int[] _stateIds;
        [System.NonSerialized] private int[] _inputIds;
        [System.NonSerialized] private bool _idsDirty = true;

        private void EnsureCachesValid()
        {
            // At edit time providers don't get OnEnable/OnDisable, so the
            // AttachSource/DetachSource path that flips _topoDirty at runtime
            // never fires. Forcing a rebuild every call is cheaper than tracking
            // edit-time invalidations explicitly — GetComponents on the host is
            // O(N) over a handful of components, and edit-time eval is cool.
            if (!Application.isPlaying)
                _topoDirty = true;

            if (!_topoDirty) return;

            _allSources.Clear();
            GetComponents(typeof(GraphSource), _scratchComponents);
            foreach (var c in _scratchComponents) _allSources.Add((IDexteritySource)c);
            GetComponents(typeof(GraphOperator), _scratchComponents);
            foreach (var c in _scratchComponents) _allSources.Add((IDexteritySource)c);

            _sourcesByPort.Clear();
            for (var i = 0; i < _allSources.Count; i++)
            {
                if (_allSources[i] is GraphOperator agg)
                    agg.incomingSources.Clear();
            }

            // Build reverse adjacency from edges.
            for (var i = 0; i < _allSources.Count; i++)
            {
                var src = _allSources[i];
                var outs = src.Outputs;
                for (var j = 0; j < outs.Count; j++)
                {
                    var edge = outs[j];
                    if (edge.target == null) continue;

                    if (ReferenceEquals(edge.target, this))
                    {
                        if (string.IsNullOrEmpty(edge.targetPort)) continue;
                        if (!_sourcesByPort.TryGetValue(edge.targetPort, out var list))
                            _sourcesByPort[edge.targetPort] = list = new List<IDexteritySource>();
                        list.Add(src);
                    }
                    else if (edge.target is GraphOperator targetAgg)
                    {
                        targetAgg.incomingSources.Add(src);
                    }
                    // else: dangling/invalid edge — silently ignored at runtime.
                }
            }

            // Topo sort (DFS) with cycle detection.
            _topoOrdered.Clear();
            _visited.Clear();
            _inProgress.Clear();
            _topoCycleDetected = false;
            for (var i = 0; i < _allSources.Count; i++)
            {
                if (!Visit(_allSources[i]))
                {
                    Debug.LogError($"Dexterity: cycle detected in GraphNode '{name}'s graph; falling back to initial state until fixed.", this);
                    _topoCycleDetected = true;
                    break;
                }
            }

            _topoDirty = false;
        }

        private readonly List<Component> _scratchComponents = new();

        private bool Visit(IDexteritySource source)
        {
            if (_visited.Contains(source)) return true;
            if (!_inProgress.Add(source)) return false;

            if (source is GraphOperator agg)
            {
                for (var i = 0; i < agg.incomingSources.Count; i++)
                {
                    if (!Visit(agg.incomingSources[i])) return false;
                }
            }

            _inProgress.Remove(source);
            _visited.Add(source);
            _topoOrdered.Add(source);
            return true;
        }

        private void EvaluateSources()
        {
            _activeCache.Clear();
            for (var i = 0; i < _topoOrdered.Count; i++)
            {
                var src = _topoOrdered[i];
                if (src is GraphOperator agg)
                    agg.RecomputeFrom(_activeCache, _aggInputScratch);
                _activeCache[src] = src.IsActive;
            }
        }

        private bool PortIsActive(string portName)
        {
            if (!_sourcesByPort.TryGetValue(portName, out var srcs)) return false;
            for (var i = 0; i < srcs.Count; i++)
            {
                if (_activeCache.TryGetValue(srcs[i], out var v) && v) return true;
            }
            return false;
        }

        // -- BaseStateNode overrides --------------------------------------------
        public override HashSet<string> GetStateNames()
        {
            // initialState + every states entry. We deliberately do NOT auto-add
            // StateFunction.kDefaultState ("<Default>") — historically that produced a
            // duplicate "default-ish" state in modifiers whenever the designer set
            // initialState to anything other than "<Default>". The initialState already
            // serves as the fallback name. inputs ports are excluded by construction so
            // modifiers don't sync inert rows for them.
            var set = new HashSet<string>();
            if (!string.IsNullOrEmpty(initialState)) set.Add(initialState);
            for (var i = 0; i < states.Count; i++)
            {
                var s = states[i];
                if (!string.IsNullOrEmpty(s)) set.Add(s);
            }
            return set;
        }

        public override HashSet<string> GetFieldNames() => IHasStates.emptySet;

        public override int GetNextStateWithoutOverride()
        {
#if UNITY_EDITOR
            // Editor final-state preview: force the resolved state (drives the preview driver's
            // modifier animation). Database is alive whenever this runs (runtime / preview driver).
            if (GraphPreviewOverrides.TryGetNodeState(this, out var forcedId) && !string.IsNullOrEmpty(forcedId))
                return Database.instance.GetStateID(forcedId);
#endif
            EnsureCachesValid();
            if (_topoCycleDetected) return initialStateId;
            EvaluateSources();
            EnsureStateIdCache();

            for (var i = 0; i < states.Count; i++)
            {
                var port = states[i];
                if (!string.IsNullOrEmpty(port) && PortIsActive(port))
                    return _stateIds[i];
            }
            return initialStateId;
        }

        /// <summary>
        /// Edit-time evaluation. Returns the state port name that would currently win, or
        /// null if none. Does not touch <see cref="Database"/> or <see cref="Manager"/>.
        /// </summary>
        public string EvaluateTreeEditor()
        {
#if UNITY_EDITOR
            // Editor final-state preview override wins (drives the winner highlight + Out-node title,
            // and any edit-time cross-node NodeStateSource reading this node).
            if (GraphPreviewOverrides.TryGetNodeState(this, out var forced))
                return string.IsNullOrEmpty(forced) ? null : forced;
#endif
            EnsureCachesValid();
            if (_topoCycleDetected) return null;
            EvaluateSources();

            for (var i = 0; i < states.Count; i++)
            {
                var port = states[i];
                if (!string.IsNullOrEmpty(port) && PortIsActive(port))
                    return port;
            }
            return null;
        }

        // -- Raw-input query API ------------------------------------------------
        /// <summary>
        /// Returns true iff any source whose edge feeds the port with the given state id is
        /// currently active, ignoring node priority. The port may be a <see cref="states"/>
        /// entry or an <see cref="inputs"/> entry. Use this when you want to react to a raw
        /// input regardless of whether a higher-priority state masks it (e.g. a click
        /// listener that fires on press even when <c>Disabled</c> wins).
        /// </summary>
        public bool GetRawInput(int stateId)
        {
            EnsureCachesValid();
            if (_topoCycleDetected) return false;
            EvaluateSources();
            EnsureStateIdCache();
            for (var i = 0; i < _stateIds.Length; i++)
                if (_stateIds[i] == stateId) return PortIsActive(states[i]);
            for (var i = 0; i < _inputIds.Length; i++)
                if (_inputIds[i] == stateId) return PortIsActive(inputs[i]);
            return false;
        }

        public bool GetRawInput(string portName)
        {
            if (string.IsNullOrEmpty(portName)) return false;
            EnsureCachesValid();
            if (_topoCycleDetected) return false;
            EvaluateSources();
            return PortIsActive(portName);
        }

        private void EnsureStateIdCache()
        {
            if (!_idsDirty
                && _stateIds != null && _stateIds.Length == states.Count
                && _inputIds != null && _inputIds.Length == inputs.Count)
                return;
            if (_stateIds == null || _stateIds.Length != states.Count)
                _stateIds = new int[states.Count];
            if (_inputIds == null || _inputIds.Length != inputs.Count)
                _inputIds = new int[inputs.Count];
            for (var i = 0; i < states.Count; i++)
            {
                var port = states[i];
                _stateIds[i] = string.IsNullOrEmpty(port) ? -1 : Database.instance.GetStateID(port);
            }
            for (var i = 0; i < inputs.Count; i++)
            {
                var port = inputs[i];
                _inputIds[i] = string.IsNullOrEmpty(port) ? -1 : Database.instance.GetStateID(port);
            }
            _idsDirty = false;
        }

        protected override void Initialize()
        {
            base.Initialize();
            _idsDirty = true;
            EnsureStateIdCache();
        }

        /// <summary>
        /// Returns the source attached to this node whose edge targets the state-input port
        /// named <paramref name="portName"/>, cast to <typeparamref name="T"/>. First matching
        /// source wins — if multiple sources feed the same port, only the first cast hit is
        /// returned. Null if no source of type <typeparamref name="T"/> feeds that port.
        ///
        /// Lets behavior code address a specific provider declaratively ("the ConstantSource
        /// feeding the IsShelf port") instead of via a serialized component reference that
        /// would duplicate the graph wiring.
        /// </summary>
        public T GetDependency<T>(string portName) where T : Component
        {
            if (string.IsNullOrEmpty(portName)) return null;
            EnsureCachesValid();
            if (_topoCycleDetected) return null;
            if (!_sourcesByPort.TryGetValue(portName, out var srcs)) return null;
            for (var i = 0; i < srcs.Count; i++)
                if (srcs[i] is T t) return t;
            return null;
        }

        /// <summary>
        /// Does this node declare a port with the given name? Covers both
        /// <see cref="states"/> and <see cref="inputs"/>.
        /// </summary>
        public bool HasInputPort(string portName)
        {
            if (string.IsNullOrEmpty(portName)) return false;
            for (var i = 0; i < states.Count; i++)
                if (states[i] == portName) return true;
            for (var i = 0; i < inputs.Count; i++)
                if (inputs[i] == portName) return true;
            return false;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            // The fallback state is always a port: if initialState isn't already a state,
            // append it at the bottom (lowest priority) so it's visible and wire-able in the
            // graph rather than an invisible implicit fallback.
            if (!string.IsNullOrEmpty(initialState) && !states.Contains(initialState))
                states.Add(initialState);

            _topoDirty = true;
            _idsDirty = true;
        }
#endif
    }
}
