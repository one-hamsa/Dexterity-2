using System;
using System.Collections.Generic;
using OneHamsa.Dexterity.Visual.Utilities;

namespace OneHamsa.Dexterity.Visual
{
    public interface ITransitionStrategy
    {
        ListDictionary<int, float> Initialize(int[] states, int currentState);

        // should be called each frame
        ListDictionary<int, float> GetTransition(ListDictionary<int, float> prevState, 
            int currentState, double timeSinceStateChange, double deltaTime, out bool changed);

        public class TransitionInitializationException : Exception { 
            public TransitionInitializationException(string message) : base(message) { }
        }

        ITransitionStrategy Clone();
    }
}
