using System;
using System.Collections.Generic;

namespace OneHamsa.Dexterity.Visual
{
    public interface IProvidesStateFunction
    {
        StateFunctionGraph stateFunctionAsset { get; }
    }
}
