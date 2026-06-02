using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Builtins
{
    /// <summary>
    /// Samples a target <see cref="GraphNode"/>'s <b>raw state-input port</b> —
    /// "is any source feeding the named port currently active?", bypassing the
    /// target's priority-respecting active state and its transition delays.
    ///
    /// Difference vs <see cref="NodeStateSource"/>:
    /// <list type="bullet">
    ///   <item><c>NodeStateSource</c>: <c>target.GetActiveState() == X</c> — waits
    ///         for the upstream node to transition before firing.</item>
    ///   <item><c>GraphInputSource</c>: <c>target.GetRawInput(X)</c> — fires the
    ///         instant the upstream signal turns on, even when masked by a higher-priority
    ///         state. Useful for dependent nodes that need to track an upstream signal
    ///         without animation lag.</item>
    /// </list>
    ///
    /// <see cref="mode"/> picks the target the same way <see cref="NodeStateSource"/>
    /// does (Reference / FirstParent / AnyChild / AllChildren).
    ///
    /// Constraint: only works with <see cref="GraphNode"/> targets (raw-input is a
    /// GraphNode-specific API). FieldNodes etc. → use <c>NodeStateSource</c>.
    /// </summary>
    [AddComponentMenu("Dexterity/Graph/Sources/Graph Input Source")]
    public class GraphInputSource : GraphSource
    {
        [Tooltip("How to pick the target node(s). Reference uses the explicit field; " +
                 "FirstParent walks up; AnyChild/AllChildren scan direct-descendant nodes.")]
        public NodeRefMode mode = NodeRefMode.Reference;

        [Tooltip("Only used when mode is Reference.")]
        public GraphNode targetNode;

        [State(objectFieldName: nameof(targetNode))]
        public string targetState;

        [Tooltip("Invert the result.")]
        public bool negate;

        private int _targetStateId = -1;
        private readonly List<GraphNode> _subscribed = new();

        protected override void OnEnable()
        {
            base.OnEnable();
            _targetStateId = Database.instance.GetStateID(targetState);
            SubscribeToTargets();
        }

        protected override void OnDisable()
        {
            UnsubscribeFromTargets();
            base.OnDisable();
        }

        private void SubscribeToTargets()
        {
            UnsubscribeFromTargets();
            foreach (var n in NodeRefDiscovery.Resolve<GraphNode>(this, mode, targetNode))
            {
                if (n == null) continue;
                n.onStateChanged += OnTargetStateChanged;
                n.onEnabled += OnTargetToggled;
                n.onDisabled += OnTargetToggled;
                _subscribed.Add(n);
            }
        }

        private void UnsubscribeFromTargets()
        {
            foreach (var n in _subscribed)
            {
                if (n == null) continue;
                n.onStateChanged -= OnTargetStateChanged;
                n.onEnabled -= OnTargetToggled;
                n.onDisabled -= OnTargetToggled;
            }
            _subscribed.Clear();
        }

        protected override bool ComputeIsActive()
        {
            bool result = mode switch
            {
                NodeRefMode.Reference   => CheckOne(targetNode),
                NodeRefMode.FirstParent => CheckOne(NodeRefDiscovery.FirstParent<GraphNode>(transform)),
                NodeRefMode.AnyChild    => CheckChildren(requireAll: false),
                NodeRefMode.AllChildren => CheckChildren(requireAll: true),
                _ => false,
            };
            return result ^ negate;
        }

        private bool CheckOne(GraphNode n)
        {
            if (n == null) return false;
            // Lazy-init targetStateId — OnEnable doesn't fire at edit time, so the
            // driver's path needs to discover the ID on demand once Database exists.
            if (_targetStateId == -1 && Database.instance != null && !string.IsNullOrEmpty(targetState))
                _targetStateId = Database.instance.GetStateID(targetState);
            if (n.initialized && _targetStateId != -1)
                return n.GetRawInput(_targetStateId);
            return n.GetRawInput(targetState);
        }

        private bool CheckChildren(bool requireAll)
        {
            bool sawAny = false;
            foreach (var n in NodeRefDiscovery.Children<GraphNode>(transform))
            {
                sawAny = true;
                bool match = CheckOne(n);
                if (requireAll && !match) return false;
                if (!requireAll && match) return true;
            }
            return requireAll && sawAny;
        }

        private void OnTargetStateChanged(int oldState, int newState) => MarkChanged();
        private void OnTargetToggled() => MarkChanged();
    }
}
