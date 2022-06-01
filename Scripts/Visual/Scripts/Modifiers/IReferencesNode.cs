using System;
using System.Collections.Generic;
using System.Linq;

namespace OneHamsa.Dexterity.Visual
{
    public interface IReferencesNode : IHasStates
    {
        DexterityBaseNode node { get; }

        IEnumerable<string> IHasStates.GetStateNames()
            => node != null ? node.GetStateNames() : Enumerable.Empty<string>();

        IEnumerable<string> IHasStates.GetFieldNames()
            => node != null ? node.GetFieldNames() : Enumerable.Empty<string>();
    }
}
