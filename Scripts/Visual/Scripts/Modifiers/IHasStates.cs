using System;
using System.Collections.Generic;

namespace OneHamsa.Dexterity.Visual
{
    public interface IHasStates
    {
        IEnumerable<string> GetStateNames();
        IEnumerable<string> GetFieldNames();
    }
}
