using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Builtins
{
    /// <summary>
    /// Reports active when the referenced node(s) are in the named state — uses
    /// <c>GetActiveState() == targetState</c>, so it respects priority and waits
    /// for transitions to complete. For instant raw-input sampling, use
    /// <see cref="GraphInputProvider"/>.
    ///
    /// <see cref="mode"/> chooses how the target node(s) are picked:
    /// Reference / FirstParent / AnyChild / AllChildren — same semantics as
    /// FieldNode's classic parent/children gates.
    /// </summary>
    [AddComponentMenu("Dexterity/Graph/Providers/Node State Provider")]
    public class NodeStateProvider : GraphStateProvider
    {
        [Tooltip("How to pick the target node(s). Reference uses the explicit field; " +
                 "FirstParent walks up; AnyChild/AllChildren scan direct-descendant nodes.")]
        public NodeRefMode mode = NodeRefMode.Reference;

        [Tooltip("Only used when mode is Reference.")]
        public BaseStateNode targetNode;

        [State(objectFieldName: nameof(targetNode))]
        public string targetState;

        [Tooltip("Invert the result.")]
        public bool negate;

        private int _targetStateId = -1;
        private readonly List<BaseStateNode> _subscribed = new();

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
            foreach (var n in ResolveTargets())
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

        private IEnumerable<BaseStateNode> ResolveTargets()
            => NodeRefDiscovery.Resolve<BaseStateNode>(this, mode, targetNode);

        protected override bool ComputeIsActive()
        {
            bool result = mode switch
            {
                NodeRefMode.Reference   => CheckOne(targetNode),
                NodeRefMode.FirstParent => CheckOne(NodeRefDiscovery.FirstParent<BaseStateNode>(transform)),
                NodeRefMode.AnyChild    => CheckChildren(requireAll: false),
                NodeRefMode.AllChildren => CheckChildren(requireAll: true),
                _ => false,
            };
            return result ^ negate;
        }

        private bool CheckOne(BaseStateNode n)
        {
            if (n == null) return false;
            // If the target is initialized (either by Manager at runtime, or by the
            // editor preview driver), use its actual activeState — that respects
            // priority, delays, and transitions. Otherwise fall back to the raw
            // edit-time evaluation (only meaningful for GraphNode targets).
            if (n.initialized)
            {
                // Lazy-init targetStateId — OnEnable doesn't fire at edit time, so the
                // driver's path needs to discover the ID on demand once Database exists.
                if (_targetStateId == -1 && Database.instance != null && !string.IsNullOrEmpty(targetState))
                    _targetStateId = Database.instance.GetStateID(targetState);
                if (_targetStateId == -1) return false;
                return n.GetActiveState() == _targetStateId;
            }
            if (n is GraphNode gn)
            {
                var current = gn.EvaluateTreeEditor() ?? gn.initialState;
                return current == targetState;
            }
            return false;
        }

        private bool CheckChildren(bool requireAll)
        {
            bool sawAny = false;
            foreach (var n in NodeRefDiscovery.Children<BaseStateNode>(transform))
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
