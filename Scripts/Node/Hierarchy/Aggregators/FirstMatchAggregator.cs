using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// Order-dependent: the first active child (in transform order) wins,
    /// and its state is passed through unchanged. Same semantics as the
    /// root <see cref="HierarchyNode"/> but applied to a sub-tree.
    /// </summary>
    [AddComponentMenu("Dexterity/Hierarchy/First Match Aggregator")]
    public class FirstMatchAggregator : HierarchyAggregator
    {
        protected override bool TryAggregate(IReadOnlyList<IHierarchyStateProvider> orderedChildren, out string result)
        {
            for (var i = 0; i < orderedChildren.Count; i++)
            {
                if (orderedChildren[i].TryGetState(out result) && !string.IsNullOrEmpty(result))
                    return true;
            }
            result = null;
            return false;
        }

        public override IEnumerable<string> GetDeclaredStates()
        {
            var set = new HashSet<string>();
            AppendChildrenDeclaredStates(set);
            return set;
        }
    }
}
