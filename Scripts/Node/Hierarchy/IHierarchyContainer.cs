using UnityEngine;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// A node or aggregator that owns child <see cref="IHierarchyStateProvider"/>s
    /// below it in the transform hierarchy. Implemented by both
    /// <see cref="HierarchyAggregator"/> and <see cref="HierarchyNode"/>.
    /// </summary>
    internal interface IHierarchyContainer
    {
        Transform transform { get; }
        void RegisterProvider(IHierarchyStateProvider provider);
        void UnregisterProvider(IHierarchyStateProvider provider);
    }
}
