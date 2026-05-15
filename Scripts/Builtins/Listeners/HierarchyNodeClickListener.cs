using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Builtins
{
    /// <summary>
    /// Click listener for a <see cref="HierarchyNode"/>. Reads its press/hover/
    /// disabled/hidden signals directly from <see cref="HierarchyStateProvider"/>
    /// components in the node's subtree, matched by declared state name.
    ///
    /// Multiple providers per role is fine: e.g. a <c>UIPressProvider</c> and
    /// a <c>RaycastPressProvider</c> both declaring <c>State = "Pressed"</c>
    /// both feed the press role (OR-of-IsActive).
    ///
    /// Works in edit mode — the graph window's override pills drive this same
    /// path, so press/click can be simulated without entering play.
    /// </summary>
    public class HierarchyNodeClickListener : BaseClickListener
    {
        [System.Serializable]
        public class Settings
        {
            [Tooltip("State name of the press provider(s) — typically \"Pressed\".")]
            public string pressedStateName = "Pressed";

            [Tooltip("State name of the hover provider(s) — typically \"Hover\".")]
            public string hoverStateName = "Hover";

            [Tooltip("Optional. Active providers with this state suppress press semantics entirely.")]
            public string disabledStateName = "Disabled";

            [Tooltip("Optional. Active providers with this state suppress press semantics entirely.")]
            public string hiddenStateName = "Hidden";
        }

        [SerializeField]
        protected HierarchyNode node;

        public Settings settings = new();

        private readonly List<HierarchyStateProvider> _pressedProviders = new();
        private readonly List<HierarchyStateProvider> _hoverProviders = new();
        private readonly List<HierarchyStateProvider> _disabledProviders = new();
        private readonly List<HierarchyStateProvider> _hiddenProviders = new();

        public HierarchyNode GetNode() => node;

        protected virtual void Awake()
        {
            if (!node) node = GetComponentInParent<HierarchyNode>();
            if (!node)
            {
                Debug.LogWarning($"HierarchyNode not found for listener ({gameObject.name})", this);
                enabled = false;
            }
        }

        protected virtual void OnEnable()
        {
            CollectProvidersByState(settings.pressedStateName,  _pressedProviders);
            CollectProvidersByState(settings.hoverStateName,    _hoverProviders);
            CollectProvidersByState(settings.disabledStateName, _disabledProviders);
            CollectProvidersByState(settings.hiddenStateName,   _hiddenProviders);

            foreach (var p in _pressedProviders)
                p.onStateMayHaveChanged += OnPressMayHaveChanged;
        }

        protected virtual void OnDisable()
        {
            foreach (var p in _pressedProviders)
                if (p != null) p.onStateMayHaveChanged -= OnPressMayHaveChanged;

            _pressedProviders.Clear();
            _hoverProviders.Clear();
            _disabledProviders.Clear();
            _hiddenProviders.Clear();
        }

        private void CollectProvidersByState(string stateName, List<HierarchyStateProvider> output)
        {
            output.Clear();
            if (string.IsNullOrEmpty(stateName)) return;
            foreach (var p in node.GetComponentsInChildren<HierarchyStateProvider>(includeInactive: true))
                if (p.State == stateName) output.Add(p);
        }

        private static bool AnyActive(List<HierarchyStateProvider> providers)
        {
            for (var i = 0; i < providers.Count; i++)
                if (providers[i].IsActive) return true;
            return false;
        }

        protected override bool IsPressed()  => AnyActive(_pressedProviders);
        protected override bool IsHover()    => _hoverProviders.Count == 0 || AnyActive(_hoverProviders);
        protected override bool IsDisabled() => AnyActive(_disabledProviders);
        protected override bool IsHidden()   => AnyActive(_hiddenProviders);
    }
}
