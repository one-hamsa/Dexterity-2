using System.Collections.Generic;

namespace OneHamsa.Dexterity.Visual
{
    public interface ITransitionStrategy
    {
        IDictionary<int, float> Initialize(int[] states, int currentState);

        // should be called each frame
        IDictionary<int, float> GetTransition(IDictionary<int, float> prevState, 
            int currentState, float stateChangeDeltaTime, out bool changed);
    }
}
