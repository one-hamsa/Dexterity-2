using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// Leaf source: a MonoBehaviour on the same GameObject as a <see cref="HierarchyNode"/>
    /// that produces a single bool output (<see cref="ComputeIsActive"/>).
    /// Anonymous — has no state name; routing is via the <see cref="outputs"/> edge list.
    ///
    /// Self-attaches to the host node's source set on <see cref="OnEnable"/>.
    /// </summary>
    [DefaultExecutionOrder(Manager.nodeExecutionPriority + 1)]
    public abstract class HierarchyStateProvider : MonoBehaviour, IDexteritySource
    {
        [SerializeField, Tooltip("Outgoing edges: where this provider's bool output feeds.")]
        protected List<DexterityEdge> outputs = new();

        [SerializeField, HideInInspector]
        protected Vector2 graphPosition;

        public event Action onStateMayHaveChanged;

        public IReadOnlyList<DexterityEdge> Outputs => outputs;

        /// <summary>Subclass logic — computes the raw bool this provider contributes.</summary>
        protected abstract bool ComputeIsActive();

        public bool IsActive
        {
            get
            {
                if (HierarchyPreviewOverrides.TryGet(this, out var overridden))
                    return overridden;

                if (!isActiveAndEnabled)
                    return false;

                return ComputeIsActive();
            }
        }

        public void NotifyExternalChanged() => MarkChanged();

        /// <summary>Subclasses call when their state may have changed.</summary>
        protected void MarkChanged() => onStateMayHaveChanged?.Invoke();

        protected virtual void OnEnable()
        {
            if (TryGetComponent<HierarchyNode>(out var node))
                node.AttachSource(this);
        }

        protected virtual void OnDisable()
        {
            if (TryGetComponent<HierarchyNode>(out var node))
                node.DetachSource(this);
            // Final notification so the node re-evaluates without our contribution.
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
