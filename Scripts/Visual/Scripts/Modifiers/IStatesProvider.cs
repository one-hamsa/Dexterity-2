using System;
using System.Collections.Generic;

namespace OneHamsa.Dexterity.Visual
{
    public interface IStatesProvider
    {
        IEnumerable<string> GetStateNames();
        IEnumerable<string> GetFieldNames();
    }
}
