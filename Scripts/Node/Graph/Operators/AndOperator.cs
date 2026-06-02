using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// Outputs <c>true</c> iff every connected input is currently active.
    /// Equivalent to a logical AND across the multiset of incoming bool sources.
    ///
    /// To wire: drop this on the same GameObject as your <see cref="GraphNode"/>,
    /// then have each contributing source/operator add a <see cref="DexterityEdge"/>
    /// pointing at this operator. The operator's own output edge(s) feed
    /// either a state-input port on the node or another operator.
    /// </summary>
    [AddComponentMenu("Dexterity/Graph/And Operator")]
    public class AndOperator : GraphOperator
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
