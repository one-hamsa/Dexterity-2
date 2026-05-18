using System;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// A source-side connection from a provider/aggregator to either the
    /// host's <see cref="HierarchyNode"/> (Out node) at a named state input port,
    /// or another <see cref="HierarchyAggregator"/> on the same GameObject.
    ///
    /// Lives in a <see cref="System.Collections.Generic.List{T}"/> on the source
    /// component. The target must be a component on the same GameObject as the
    /// source — Dexterity does not support cross-GameObject edges (cross-node
    /// dependencies go through <c>NodeStateProvider</c>).
    /// </summary>
    [Serializable]
    public struct DexterityEdge
    {
        /// <summary>Target component on the host GameObject. Must be a HierarchyNode or HierarchyAggregator.</summary>
        public Component target;

        /// <summary>
        /// When target is a <see cref="HierarchyNode"/>, the state-input port name to feed.
        /// When target is a <see cref="HierarchyAggregator"/>, ignored (aggregators consume their
        /// incoming sources as a multiset of bools).
        /// </summary>
        public string targetPort;
    }
}
