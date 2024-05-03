using System;
using System.Collections.Generic;
using System.Linq;

namespace OneHamsa.Dexterity
{
    public interface IReferencesNodes : IHasStates
    {
        List<BaseStateNode> GetNodes();

        HashSet<string> IHasStates.GetStateNames()
        {
            var nodes = GetNodes();
            if (nodes == null || nodes.Count == 0)
                return emptySet;
            
            var stateNames = new HashSet<string>();
            foreach (var node in nodes)
            {
                stateNames.UnionWith(node.GetStateNames());
            }
            return stateNames;
        }
    }
}
