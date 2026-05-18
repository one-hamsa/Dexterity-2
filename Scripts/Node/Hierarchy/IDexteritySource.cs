using System;
using System.Collections.Generic;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// Anything that produces a single bool output and can feed a <see cref="HierarchyNode"/>
    /// or a <see cref="HierarchyAggregator"/> via a <see cref="DexterityEdge"/>.
    /// Implemented by <see cref="HierarchyStateProvider"/> (leaves) and
    /// <see cref="HierarchyAggregator"/> (intermediates).
    /// </summary>
    public interface IDexteritySource
    {
        /// <summary>Current bool output, override-aware (consults <see cref="HierarchyPreviewOverrides"/>).</summary>
        bool IsActive { get; }

        /// <summary>Outgoing edges from this source.</summary>
        IReadOnlyList<DexterityEdge> Outputs { get; }

        /// <summary>Fires when <see cref="IsActive"/> may have changed. Not guaranteed to fire only on actual change.</summary>
        event Action onStateMayHaveChanged;

        /// <summary>External hook (override registry / editor tooling) — forces a re-eval signal.</summary>
        void NotifyExternalChanged();
    }
}
