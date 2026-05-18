using UnityEngine;

namespace OneHamsa.Dexterity.Builtins
{
    /// <summary>
    /// Click listener for a <see cref="GraphNode"/>. Reads press/hover/disabled/hidden
    /// signals from the node's raw inputs (priority-independent) via
    /// <see cref="GraphNode.GetRawInput(string)"/>.
    ///
    /// "Raw" means: the listener fires on press even when a higher-priority state
    /// like <c>Disabled</c> currently masks the node's active state. If you want
    /// priority-respecting behavior (don't react when disabled wins), prefer
    /// <see cref="GraphNode.GetActiveState"/> directly in a custom listener.
    ///
    /// A state name not declared on the node is treated as "no provider for that role".
    /// Hover is permissive (no hover providers → treated as always-hovered) to preserve
    /// the historical behavior of this listener.
    /// </summary>
    public class GraphNodeClickListener : BaseClickListener
    {
        [System.Serializable]
        public class Settings
        {
            [Tooltip("State name fed by press providers — typically \"Pressed\".")]
            public string pressedStateName = "Pressed";

            [Tooltip("State name fed by hover providers — typically \"Hover\".")]
            public string hoverStateName = "Hover";

            [Tooltip("State name fed by disabled providers — suppresses press semantics.")]
            public string disabledStateName = "Disabled";

            [Tooltip("State name fed by hidden providers — suppresses press semantics.")]
            public string hiddenStateName = "Hidden";
        }

        [SerializeField]
        protected GraphNode node;

        public Settings settings = new();

        public GraphNode GetNode() => node;

        protected virtual void Awake()
        {
            if (!node) node = GetComponentInParent<GraphNode>();
            if (!node)
            {
                Debug.LogWarning($"GraphNode not found for listener ({gameObject.name})", this);
                enabled = false;
            }
        }

        // Empty hooks so subclasses (GraphNodeLongPressListener) can override safely.
        // The new model doesn't need provider subscription here — node.GetRawInput is polled.
        protected virtual void OnEnable() { }
        protected virtual void OnDisable() { }

        protected override bool IsPressed() => node != null && node.GetRawInput(settings.pressedStateName);

        protected override bool IsHover()
        {
            if (node == null) return false;
            // Preserve historical permissive behavior: if no provider declares hover,
            // treat as always-hovered (e.g. a non-spatial UI button has no hover input).
            return !node.HasInputPort(settings.hoverStateName) || node.GetRawInput(settings.hoverStateName);
        }

        protected override bool IsDisabled() => node != null && node.GetRawInput(settings.disabledStateName);
        protected override bool IsHidden()   => node != null && node.GetRawInput(settings.hiddenStateName);
    }
}
