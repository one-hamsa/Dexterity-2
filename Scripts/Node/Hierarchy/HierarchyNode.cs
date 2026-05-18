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
    [AddComponentMenu("Dexterity/Hierarchy Node")]
    [DefaultExecutionOrder(Manager.nodeExecutionPriority)]
    public class HierarchyNode : BaseStateNode
    {
        [SerializeField, Tooltip("Ordered state inputs. Port name = state name. First port with any active source wins.")]
        private List<string> stateInputs = new();

        [SerializeField, HideInInspector]
        private Vector2 graphPosition;

        // -- Source management (sources self-attach on enable) -----------------
        private readonly HashSet<IDexteritySource> _attached = new();

        internal void AttachSource(IDexteritySource source)
        {
            if (_attached.Add(source))
            {
                source.onStateMayHaveChanged += MarkStateDirty;
                _topoDirty = true;
                MarkStateDirty();
            }
        }

        internal void DetachSource(IDexteritySource source)
        {
            if (_attached.Remove(source))
            {
                source.onStateMayHaveChanged -= MarkStateDirty;
                _topoDirty = true;
                MarkStateDirty();
            }
        }

        private void MarkStateDirty() => stateDirty = true;

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

        private readonly HashSet<IDexteritySource> _liveScratch = new();

        private void EnsureCachesValid()
        {
            // Self-heal: at edit time, providers/aggregators don't get OnEnable/OnDisable
            // (no [ExecuteAlways]), so AttachSource/DetachSource isn't called. Re-collect
            // host-local sources every call and invalidate if the live set differs from
            // what we have cached (or if anything we held went null/destroyed).
            _liveScratch.Clear();
            GetComponents(typeof(HierarchyStateProvider), _scratchComponents);
            foreach (var c in _scratchComponents) if (c != null) _liveScratch.Add((IDexteritySource)c);
            GetComponents(typeof(HierarchyAggregator), _scratchComponents);
            foreach (var c in _scratchComponents) if (c != null) _liveScratch.Add((IDexteritySource)c);

            if (!_topoDirty)
            {
                if (_liveScratch.Count != _allSources.Count) _topoDirty = true;
                else
                {
                    for (var i = 0; i < _allSources.Count; i++)
                    {
                        var s = _allSources[i];
                        if (s == null || !(s is UnityEngine.Object u) || u == null || !_liveScratch.Contains(s))
                        { _topoDirty = true; break; }
                    }
                }
            }
            if (!_topoDirty) return;

            _allSources.Clear();
            foreach (var s in _liveScratch) _allSources.Add(s);

            _sourcesByPort.Clear();
            for (var i = 0; i < _allSources.Count; i++)
            {
                if (_allSources[i] is HierarchyAggregator agg)
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
                    else if (edge.target is HierarchyAggregator targetAgg)
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
                    Debug.LogError($"Dexterity: cycle detected in HierarchyNode '{name}'s graph; falling back to initial state until fixed.", this);
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

            if (source is HierarchyAggregator agg)
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
                if (src is HierarchyAggregator agg)
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
            var set = new HashSet<string>();
            set.Add(initialState);
            set.Add(StateFunction.kDefaultState);
            for (var i = 0; i < stateInputs.Count; i++)
            {
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

            for (var i = 0; i < stateInputs.Count; i++)
            {
                var port = stateInputs[i];
                if (!string.IsNullOrEmpty(port) && PortIsActive(port))
                    return Database.instance.GetStateID(port);
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
            => GetRawInput(Database.instance.GetStateAsString(stateId));

        public bool GetRawInput(string portName)
        {
            if (string.IsNullOrEmpty(portName)) return false;
            EnsureCachesValid();
            if (_topoCycleDetected) return false;
            EvaluateSources();
            return PortIsActive(portName);
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

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            _topoDirty = true;
        }
#endif
    }
}
