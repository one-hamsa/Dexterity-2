using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// Leaf provider: declares a single state string and, via subclass logic,
    /// decides whether it currently contributes that state to its owning node.
    /// Drop on any GameObject under a <see cref="HierarchyNode"/> (or under an
    /// intermediate <see cref="HierarchyAggregator"/>) — self-registers on enable.
    /// </summary>
    [DefaultExecutionOrder(Manager.nodeExecutionPriority + 1)]
    public abstract class HierarchyStateProvider : MonoBehaviour, IHierarchyStateProvider
    {
        [SerializeField, Tooltip("State name this provider reports when active. Free text.")]
        private string state;

        private readonly string[] _declared = new string[1];
        private IHierarchyContainer _container;

        public string State => state;

        public event Action onStateMayHaveChanged;

        /// <summary>
        /// Subclasses return whether they're currently contributing their state
        /// (e.g., a HoverProvider returns its hover input's current value).
        /// Called at runtime only.
        /// </summary>
        protected abstract bool ComputeIsActive();

        public bool IsActive
        {
            get
            {
                if (HierarchyPreviewOverrides.TryGet(this, out var overridden))
                    return overridden;

                if (!isActiveAndEnabled)
                    return false;

                // Subclasses are responsible for edit-time safety in ComputeIsActive
                // (return false when their inputs aren't wired). Most do so trivially
                // because their state fields default to inactive.
                return ComputeIsActive();
            }
        }

        /// <summary>
        /// External hook (override registry / editor tooling) — fires
        /// <see cref="onStateMayHaveChanged"/> without needing subclass cooperation.
        /// </summary>
        internal void NotifyExternalChanged() => MarkChanged();

        public bool TryGetState(out string state)
        {
            state = this.state;
            return IsActive;
        }

        public IEnumerable<string> GetDeclaredStates()
        {
            _declared[0] = state;
            return _declared;
        }

        /// <summary>
        /// Subclasses call this whenever their active/state may have changed.
        /// </summary>
        protected void MarkChanged() => onStateMayHaveChanged?.Invoke();

        protected virtual void OnEnable()
        {
            _container = HierarchyUtils.FindNearestContainer(transform);
            _container?.RegisterProvider(this);
        }

        protected virtual void OnDisable()
        {
            _container?.UnregisterProvider(this);
            _container = null;
            // notify anyone still subscribed that our contribution effectively went away
            onStateMayHaveChanged?.Invoke();
        }

        protected virtual void OnTransformParentChanged()
        {
            if (!isActiveAndEnabled) return;
            _container?.UnregisterProvider(this);
            _container = HierarchyUtils.FindNearestContainer(transform);
            _container?.RegisterProvider(this);
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
