using System;
using System.Collections.Generic;

namespace OneHamsa.Dexterity.Visual
{
    public interface ITransitionStrategy
    {
        IDictionary<int, float> Initialize(int[] states, int currentState);

        // should be called each frame
        IDictionary<int, float> GetTransition(IDictionary<int, float> prevState, 
            int currentState, double timeSinceStateChange, double deltaTime, out bool changed);

        public class TransitionInitializationException : Exception { 
            public TransitionInitializationException(string message) : base(message) { }
        }
    }
}
