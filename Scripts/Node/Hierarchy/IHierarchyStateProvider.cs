using System;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// Implemented by anything that can contribute a state to a <see cref="HierarchyNode"/>:
    /// leaf <see cref="HierarchyStateProvider"/>s and branch <see cref="HierarchyAggregator"/>s.
    /// </summary>
    public interface IHierarchyStateProvider
    {
        /// <summary>
        /// When false, this provider contributes nothing this evaluation.
        /// When true, <paramref name="state"/> is the state name it produces.
        /// </summary>
        bool TryGetState(out string state);

        /// <summary>
        /// All state names this provider can ever produce. Used to populate the
        /// owning node's <c>GetStateNames</c>.
        /// </summary>
        System.Collections.Generic.IEnumerable<string> GetDeclaredStates();

        /// <summary>
        /// Fired when active/state may have changed. Subscribers should
        /// re-evaluate; not guaranteed to fire only on actual change.
        /// </summary>
        event Action onStateMayHaveChanged;
    }
}
