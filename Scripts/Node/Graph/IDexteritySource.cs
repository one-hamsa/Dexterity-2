using System;
using System.Collections.Generic;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// Anything that produces a single bool output and can feed a <see cref="GraphNode"/>
    /// or a <see cref="GraphAggregator"/> via a <see cref="DexterityEdge"/>.
    /// Implemented by <see cref="GraphStateProvider"/> (leaves) and
    /// <see cref="GraphAggregator"/> (intermediates).
    /// </summary>
    public interface IDexteritySource
    {
        /// <summary>Current bool output, override-aware (consults <see cref="GraphPreviewOverrides"/>).</summary>
        bool IsActive { get; }

        /// <summary>Outgoing edges from this source.</summary>
        IReadOnlyList<DexterityEdge> Outputs { get; }

        /// <summary>Fires when <see cref="IsActive"/> may have changed. Not guaranteed to fire only on actual change.</summary>
        event Action onStateMayHaveChanged;

        /// <summary>External hook (override registry / editor tooling) — forces a re-eval signal.</summary>
        void NotifyExternalChanged();
    }
}
