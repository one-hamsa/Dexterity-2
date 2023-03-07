using System;
using System.Collections.Generic;
using System.Linq;

namespace OneHamsa.Dexterity.Visual
{
    public interface IReferencesNode : IHasStates
    {
        DexterityBaseNode GetNode();

        IEnumerable<string> IHasStates.GetStateNames()
            => GetNode() != null ? GetNode().GetStateNames() : Enumerable.Empty<string>();

        IEnumerable<string> IHasStates.GetFieldNames()
            => GetNode() != null ? GetNode().GetFieldNames() : Enumerable.Empty<string>();
    }
}
