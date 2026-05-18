using UnityEngine;

namespace OneHamsa.Dexterity.Builtins
{
    /// <summary>
    /// GraphNode equivalent of <see cref="NodeStateField"/>.
    /// Reports its state while the referenced <see cref="BaseStateNode"/> is in
    /// the named state. Event-driven; no polling.
    /// </summary>
    [AddComponentMenu("Dexterity/Graph/Providers/Node State Provider")]
    public class NodeStateProvider : GraphStateProvider
    {
        public BaseStateNode targetNode;

        [State(objectFieldName: nameof(targetNode))]
        public string targetState;

        [Tooltip("Invert the equality result.")]
        public bool negate;

        private int _targetStateId = -1;
        private bool _subscribed;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (targetNode == null) return;

            _targetStateId = Database.instance.GetStateID(targetState);

            targetNode.onStateChanged += OnTargetStateChanged;
            targetNode.onEnabled += OnTargetToggled;
            targetNode.onDisabled += OnTargetToggled;
            _subscribed = true;
        }

        protected override void OnDisable()
        {
            if (_subscribed && targetNode != null)
            {
                targetNode.onStateChanged -= OnTargetStateChanged;
                targetNode.onEnabled -= OnTargetToggled;
                targetNode.onDisabled -= OnTargetToggled;
            }
            _subscribed = false;
            base.OnDisable();
        }

        protected override bool ComputeIsActive()
        {
            if (targetNode == null)
                return negate;

            if (Application.isPlaying)
            {
                // Runtime path: int ID comparison (fast, Database is alive).
                if (!targetNode.initialized || _targetStateId == -1)
                    return negate;
                return (targetNode.GetActiveState() == _targetStateId) ^ negate;
            }

            // Edit-time path: no Database, no init. Compare by string against
            // the target's tree evaluation. Only GraphNodes support this.
            if (targetNode is GraphNode hn)
            {
                var current = hn.EvaluateTreeEditor() ?? hn.initialState;
                return (current == targetState) ^ negate;
            }

            return negate;
        }

        private void OnTargetStateChanged(int oldState, int newState) => MarkChanged();
        private void OnTargetToggled() => MarkChanged();
    }
}
