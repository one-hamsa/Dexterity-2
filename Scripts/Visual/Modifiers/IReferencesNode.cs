using System;
using System.Collections.Generic;
using System.Linq;

namespace OneHamsa.Dexterity.Visual
{
    public interface IReferencesNode : IHasStates
    {
        DexterityBaseNode GetNode();

        HashSet<string> IHasStates.GetStateNames()
            => GetNode() != null ? GetNode().GetStateNames() : emptySet;

        HashSet<string> IHasStates.GetFieldNames()
            => GetNode() != null ? GetNode().GetFieldNames() : emptySet;
    }
}
