using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// Outputs <c>true</c> iff every connected input is currently active.
    /// Equivalent to a logical AND across the multiset of incoming bool sources.
    ///
    /// To wire: drop this on the same GameObject as your <see cref="GraphNode"/>,
    /// then have each contributing provider/aggregator add a <see cref="DexterityEdge"/>
    /// pointing at this aggregator. The aggregator's own output edge(s) feed
    /// either a state-input port on the node or another aggregator.
    /// </summary>
    [AddComponentMenu("Dexterity/Graph/And Aggregator")]
    public class AndAggregator : GraphAggregator
    {
        protected override bool ComputeOutput(IReadOnlyList<bool> inputs)
        {
            if (inputs.Count == 0) return false;
            for (var i = 0; i < inputs.Count; i++)
            {
                if (!inputs[i]) return false;
            }
            return true;
        }
    }
}
