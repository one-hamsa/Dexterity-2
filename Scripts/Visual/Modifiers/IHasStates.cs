using System;
using System.Collections.Generic;

namespace OneHamsa.Dexterity.Visual
{
    public interface IHasStates
    {
        static readonly HashSet<string> emptySet = new(0);
        
        HashSet<string> GetStateNames();
        HashSet<string> GetFieldNames();
    }
}
