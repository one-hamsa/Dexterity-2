using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// State node whose state is decided by an explicit list of named state inputs
    /// (the "Out node" of a Dexterity graph). Providers and aggregators living as
    /// components on the SAME GameObject feed bool signals into named state-input
    /// ports via their <see cref="DexterityEdge"/> output list.
    ///
    /// Evaluation: iterate <see cref="stateInputs"/> in order; the first port with
    /// any active source feeding it wins. If no port is active, falls back to
    /// <see cref="BaseStateNode.initialState"/>.
    ///
    /// Aggregators are computed in topological order (their inputs are resolved
    /// before they are). Cycles fall back to <see cref="BaseStateNode.initialState"/>
    /// with an error log.
    /// </summary>
    [AddComponentMenu("Dexterity/Graph Node")]
    [DefaultExecutionOrder(Manager.nodeExecutionPriority)]
    public class GraphNode : BaseStateNode
    {
        [SerializeField, Tooltip("Ordered state inputs. Port name = state name. First port with any active source wins.")]
        private List<string> stateInputs = new();

        // Parallel to stateInputs (same index, same length — enforced in OnValidate and
        // EnsureRawOnlyListSize). A "raw-only" port is wire-able in the graph window so
        // providers can edge into it and listeners can read its raw value via
        // GetRawInput, but it is INVISIBLE to modifiers (GetStateNames skips it) and
        // can never become the priority-resolved active state (GetNextStateWithoutOverride
        // and EvaluateTreeEditor skip it). The canonical use case is a click listener
        // reading raw Pressed/Hover signals on a toggle button whose "real" states are
        // the mode-gated combinations (Shelf/ShelfHover/ShelfPress + Drop/...).
        [SerializeField, HideInInspector]
        private List<bool> stateInputsRawOnly = new();

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

        // Runtime-only: state-input port names resolved to Database int IDs.
        // Built lazily on first runtime access (Database may not be alive at edit time).
        // Parallel to stateInputs (same index, length == stateInputs.Count).
        [System.NonSerialized] private int[] _stateInputIds;
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
            GetComponents(typeof(GraphStateProvider), _scratchComponents);
            foreach (var c in _scratchComponents) _allSources.Add((IDexteritySource)c);
            GetComponents(typeof(GraphAggregator), _scratchComponents);
            foreach (var c in _scratchComponents) _allSources.Add((IDexteritySource)c);

            _sourcesByPort.Clear();
            for (var i = 0; i < _allSources.Count; i++)
            {
                if (_allSources[i] is GraphAggregator agg)
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
                    else if (edge.target is GraphAggregator targetAgg)
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

            if (source is GraphAggregator agg)
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
                if (src is GraphAggregator agg)
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
            // initialState + every NON-raw-only stateInputs entry. We deliberately do
            // NOT auto-add StateFunction.kDefaultState ("<Default>") — historically that
            // produced a duplicate "default-ish" state in modifiers whenever the
            // designer set initialState to anything other than "<Default>". The
            // initialState already serves as the fallback name. Raw-only ports are
            // excluded so modifiers don't sync inert rows for them.
            var set = new HashSet<string>();
            if (!string.IsNullOrEmpty(initialState)) set.Add(initialState);
            for (var i = 0; i < stateInputs.Count; i++)
            {
                if (IsRawOnly(i)) continue;
                var s = stateInputs[i];
                if (!string.IsNullOrEmpty(s)) set.Add(s);
            }
            return set;
        }

        public override HashSet<string> GetFieldNames() => IHasStates.emptySet;

        public override int GetNextStateWithoutOverride()
        {
            EnsureCachesValid();
            if (_topoCycleDetected) return initialStateId;
            EvaluateSources();
            EnsureStateIdCache();

            for (var i = 0; i < stateInputs.Count; i++)
            {
                if (IsRawOnly(i)) continue; // raw-only ports never become the active state
                var port = stateInputs[i];
                if (!string.IsNullOrEmpty(port) && PortIsActive(port))
                    return _stateInputIds[i];
            }
            return initialStateId;
        }

        /// <summary>
        /// Edit-time evaluation. Returns the state-input port name that would currently
        /// win, or null if none. Does not touch <see cref="Database"/> or <see cref="Manager"/>.
        /// </summary>
        public string EvaluateTreeEditor()
        {
            EnsureCachesValid();
            if (_topoCycleDetected) return null;
            EvaluateSources();

            for (var i = 0; i < stateInputs.Count; i++)
            {
                if (IsRawOnly(i)) continue; // raw-only ports never become the active state
                var port = stateInputs[i];
                if (!string.IsNullOrEmpty(port) && PortIsActive(port))
                    return port;
            }
            return null;
        }

        // -- Raw-input query API ------------------------------------------------
        /// <summary>
        /// Returns true iff any source whose edge feeds the given state-input port is
        /// currently active, ignoring node priority. Use this when you want to react
        /// to a raw input regardless of whether a higher-priority state masks it
        /// (e.g. a click listener that fires on press even when <c>Disabled</c> wins).
        /// </summary>
        public bool GetRawInput(int stateId)
        {
            EnsureCachesValid();
            if (_topoCycleDetected) return false;
            EvaluateSources();
            EnsureStateIdCache();
            for (var i = 0; i < _stateInputIds.Length; i++)
            {
                if (_stateInputIds[i] == stateId) return PortIsActive(stateInputs[i]);
            }
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
            if (!_idsDirty && _stateInputIds != null && _stateInputIds.Length == stateInputs.Count) return;
            if (_stateInputIds == null || _stateInputIds.Length != stateInputs.Count)
                _stateInputIds = new int[stateInputs.Count];
            for (var i = 0; i < stateInputs.Count; i++)
            {
                var port = stateInputs[i];
                _stateInputIds[i] = string.IsNullOrEmpty(port) ? -1 : Database.instance.GetStateID(port);
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
        /// Lets behavior code address a specific provider declaratively ("the ConstantProvider
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

        /// <summary>Does this node declare a state-input port with the given name?</summary>
        public bool HasInputPort(string portName)
        {
            if (string.IsNullOrEmpty(portName)) return false;
            for (var i = 0; i < stateInputs.Count; i++)
            {
                if (stateInputs[i] == portName) return true;
            }
            return false;
        }

        /// <summary>
        /// Is the port at the given <c>stateInputs</c> index marked raw-only? Raw-only ports
        /// are excluded from <see cref="GetStateNames"/> (so modifiers don't sync rows for
        /// them) and from priority resolution in <see cref="GetNextStateWithoutOverride"/> /
        /// <see cref="EvaluateTreeEditor"/> (so they can never be the active state), but
        /// they remain wire-able and readable via <see cref="GetRawInput(string)"/>.
        /// </summary>
        public bool IsRawOnly(int index)
        {
            // Tolerate a short rawOnly list — treat missing entries as false. OnValidate
            // pads the list to match stateInputs in the editor, but this guard means
            // runtime code stays correct even when called before OnValidate has run
            // (e.g. immediately after AddComponent in a procedural builder).
            return index >= 0 && index < stateInputsRawOnly.Count && stateInputsRawOnly[index];
        }

        /// <summary>Convenience: is the named port marked raw-only?</summary>
        public bool IsPortRawOnly(string portName)
        {
            if (string.IsNullOrEmpty(portName)) return false;
            for (var i = 0; i < stateInputs.Count; i++)
            {
                if (stateInputs[i] == portName) return IsRawOnly(i);
            }
            return false;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            // Keep the parallel raw-only list aligned with stateInputs so reads by index
            // remain meaningful. Pad with false for new entries; truncate when stateInputs
            // shrinks. (Reordering is the editor's responsibility — see GraphNodeEditor.)
            while (stateInputsRawOnly.Count < stateInputs.Count) stateInputsRawOnly.Add(false);
            if (stateInputsRawOnly.Count > stateInputs.Count)
                stateInputsRawOnly.RemoveRange(stateInputs.Count,
                    stateInputsRawOnly.Count - stateInputs.Count);

            _topoDirty = true;
            _idsDirty = true;
        }
#endif
    }
}
