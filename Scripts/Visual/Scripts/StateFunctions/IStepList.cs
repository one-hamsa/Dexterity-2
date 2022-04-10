using System.Collections.Generic;

namespace OneHamsa.Dexterity.Visual
{
    public interface IStepList
    {
        List<StateFunction.Step> steps { get; }
    }
}