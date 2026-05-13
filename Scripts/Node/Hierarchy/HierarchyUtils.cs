using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    internal static class HierarchyUtils
    {
        /// <summary>
        /// Walks up from <paramref name="from"/>'s parent and returns the nearest
        /// container (aggregator or node). Mirrors <c>Modifier.TryFindNode</c>.
        /// </summary>
        internal static IHierarchyContainer FindNearestContainer(Transform from)
        {
            var t = from.parent;
            while (t != null)
            {
                if (t.TryGetComponent<HierarchyAggregator>(out var agg)) return agg;
                if (t.TryGetComponent<HierarchyNode>(out var node)) return node;
                t = t.parent;
            }
            return null;
        }

        /// <summary>
        /// Walks the transform subtree of <paramref name="root"/> in depth-first
        /// sibling order, collecting <see cref="IHierarchyStateProvider"/>s whose
        /// nearest enclosing container is <paramref name="root"/> itself.
        /// Recursion stops at descendants that are themselves containers
        /// (they're collected as a single provider but their subtree is skipped).
        /// </summary>
        internal static void CollectOrderedDirectProviders(
            Transform root, List<IHierarchyStateProvider> output)
        {
            CollectRecursive(root, output);
        }

        private static void CollectRecursive(Transform t, List<IHierarchyStateProvider> output)
        {
            var count = t.childCount;
            for (var i = 0; i < count; i++)
            {
                var child = t.GetChild(i);

                // a child that is itself a container "absorbs" its descendants;
                // include it as a single provider (it's a HierarchyAggregator) and skip recursion.
                if (child.TryGetComponent<HierarchyAggregator>(out var agg))
                {
                    output.Add(agg);
                    continue;
                }

                // HierarchyNode acts as a container too: anything below another node
                // belongs to that other node, not to us — skip the whole subtree
                // and don't include the node as a provider (a node is not a provider).
                if (child.TryGetComponent<HierarchyNode>(out _))
                    continue;

                if (child.TryGetComponent<HierarchyStateProvider>(out var leaf))
                    output.Add(leaf);

                CollectRecursive(child, output);
            }
        }
    }
}
