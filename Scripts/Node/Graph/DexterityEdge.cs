using System;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// A source-side connection from a provider/aggregator to either the
    /// host's <see cref="GraphNode"/> (Out node) at a named state input port,
    /// or another <see cref="GraphOperator"/> on the same GameObject.
    ///
    /// Lives in a <see cref="System.Collections.Generic.List{T}"/> on the source
    /// component. The target must be a component on the same GameObject as the
    /// source — Dexterity does not support cross-GameObject edges (cross-node
    /// dependencies go through <c>NodeStateSource</c>).
    /// </summary>
    [Serializable]
    public struct DexterityEdge
    {
        /// <summary>Target component on the host GameObject. Must be a GraphNode or GraphOperator.</summary>
        public Component target;

        /// <summary>
        /// When target is a <see cref="GraphNode"/>, the state-input port name to feed.
        /// When target is a <see cref="GraphOperator"/>, ignored (aggregators consume their
        /// incoming sources as a multiset of bools).
        /// </summary>
        public string targetPort;
    }
}
