using System;
using System.Collections.Generic;

namespace OneHamsa.Dexterity
{
    public interface ITransitionStrategy
    {
        SortedList<int, float> Initialize(int[] states, int currentState);

        // should be called each frame
        SortedList<int, float> GetTransition(SortedList<int, float> prevState, 
            int currentState, double timeSinceStateChange, double deltaTime, out bool changed);

        public class TransitionInitializationException : Exception { 
            public TransitionInitializationException(string message) : base(message) { }
        }
    }
}
