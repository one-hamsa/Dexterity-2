using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// Intermediate source: combines its incoming bool inputs (from other sources
    /// whose <see cref="DexterityEdge"/>s target this aggregator) into a single
    /// bool output via subclass-defined <see cref="ComputeOutput"/>.
    ///
    /// Anonymous — no state name. Routing is via the <see cref="outputs"/> edge list.
    /// Self-attaches to the host node's source set on <see cref="OnEnable"/>.
    ///
    /// Aggregators have no named input ports — they consume the multiset of bools
    /// from whichever sources happen to point at them. If a future use case requires
    /// labeled inputs, the schema can grow then.
    /// </summary>
    [DefaultExecutionOrder(Manager.nodeExecutionPriority + 1)]
    public abstract class GraphAggregator : MonoBehaviour, IDexteritySource
    {
        [SerializeField, Tooltip("Outgoing edges: where this aggregator's bool output feeds.")]
        protected List<DexterityEdge> outputs = new();

        [SerializeField, HideInInspector]
        protected Vector2 graphPosition;

        public event Action onStateMayHaveChanged;

        public IReadOnlyList<DexterityEdge> Outputs => outputs;

        /// <summary>
        /// Subclass strategy — combine the current bool inputs into this aggregator's bool output.
        /// Inputs are the IsActive values of all sources whose edge targets this aggregator,
        /// in the order the host's evaluator topologically resolved them (stable but unspecified).
        /// </summary>
        protected abstract bool ComputeOutput(IReadOnlyList<bool> inputs);

        // Re-evaluation state — managed by GraphNode during its eval pass.
        [NonSerialized] internal List<IDexteritySource> incomingSources = new();
        [NonSerialized] private bool _cachedOutput;

        public bool IsActive
        {
            get
            {
                if (GraphPreviewOverrides.TryGet(this, out var overridden))
                    return overridden;

                if (!isActiveAndEnabled)
                    return false;

                return _cachedOutput;
            }
        }

        /// <summary>
        /// Called by <see cref="GraphNode"/> in topological order during evaluation.
        /// <paramref name="cache"/> contains pre-computed IsActive for every source already
        /// processed in this pass (including this aggregator's inputs).
        /// </summary>
        internal void RecomputeFrom(Dictionary<IDexteritySource, bool> cache, List<bool> scratch)
        {
            scratch.Clear();
            for (var i = 0; i < incomingSources.Count; i++)
            {
                var src = incomingSources[i];
                if (src == null) continue;
                scratch.Add(cache.TryGetValue(src, out var v) && v);
            }
            _cachedOutput = ComputeOutput(scratch);
        }

        public void NotifyExternalChanged() => onStateMayHaveChanged?.Invoke();

        protected virtual void OnEnable()
        {
            if (TryGetComponent<GraphNode>(out var node))
                node.AttachSource(this);
        }

        protected virtual void OnDisable()
        {
            if (TryGetComponent<GraphNode>(out var node))
                node.DetachSource(this);
            onStateMayHaveChanged?.Invoke();
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (!Application.isPlaying)
                onStateMayHaveChanged?.Invoke();
        }
#endif
    }
}
