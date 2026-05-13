using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// Branch node in the hierarchy tree: combines its direct child providers
    /// (via subclass-defined logic) into a single state, and acts as a provider
    /// to its own parent container.
    /// </summary>
    [DefaultExecutionOrder(Manager.nodeExecutionPriority + 1)]
    public abstract class HierarchyAggregator : MonoBehaviour, IHierarchyStateProvider, IHierarchyContainer
    {
        private readonly HashSet<IHierarchyStateProvider> _registered = new();
        private readonly List<IHierarchyStateProvider> _scratch = new();
        private IHierarchyContainer _container;

        public event Action onStateMayHaveChanged;

        public bool TryGetState(out string state)
        {
            _scratch.Clear();
            HierarchyUtils.CollectOrderedDirectProviders(transform, _scratch);
            return TryAggregate(_scratch, out state);
        }

        /// <summary>
        /// Subclass strategy. Returns true with <paramref name="result"/> set if this
        /// aggregator currently contributes a state; false otherwise.
        /// </summary>
        protected abstract bool TryAggregate(IReadOnlyList<IHierarchyStateProvider> orderedChildren, out string result);

        /// <summary>
        /// All state names this aggregator can ever output. Used to populate the node's state list.
        /// </summary>
        public abstract IEnumerable<string> GetDeclaredStates();

        /// <summary>
        /// Helper for aggregators that pass child states through: collects declared
        /// states from all direct child providers in this aggregator's subtree.
        /// </summary>
        protected void AppendChildrenDeclaredStates(HashSet<string> output)
        {
            var list = new List<IHierarchyStateProvider>();
            HierarchyUtils.CollectOrderedDirectProviders(transform, list);
            foreach (var p in list)
            {
                if (p == null) continue;
                foreach (var s in p.GetDeclaredStates())
                    output.Add(s);
            }
        }

        #region IHierarchyContainer
        void IHierarchyContainer.RegisterProvider(IHierarchyStateProvider provider)
        {
            if (_registered.Add(provider))
            {
                provider.onStateMayHaveChanged += OnChildChanged;
                OnChildChanged();
            }
        }

        void IHierarchyContainer.UnregisterProvider(IHierarchyStateProvider provider)
        {
            if (_registered.Remove(provider))
            {
                provider.onStateMayHaveChanged -= OnChildChanged;
                OnChildChanged();
            }
        }
        #endregion

        private void OnChildChanged() => onStateMayHaveChanged?.Invoke();

        protected virtual void OnEnable()
        {
            _container = HierarchyUtils.FindNearestContainer(transform);
            _container?.RegisterProvider(this);
        }

        protected virtual void OnDisable()
        {
            _container?.UnregisterProvider(this);
            _container = null;

            // unsubscribe from any still-registered children to avoid leaks
            foreach (var p in _registered)
                p.onStateMayHaveChanged -= OnChildChanged;
            _registered.Clear();

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
