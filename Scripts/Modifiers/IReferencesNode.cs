using System;
using System.Collections.Generic;
using System.Linq;

namespace OneHamsa.Dexterity
{
    public interface IReferencesNode : IHasStates
    {
        BaseStateNode GetNode();

        HashSet<string> IHasStates.GetStateNames()
            => GetNode() != null ? GetNode().GetStateNames() : emptySet;
    }
}
