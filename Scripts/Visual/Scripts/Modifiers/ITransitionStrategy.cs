using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    public interface ITransitionStrategy
    {
        Dictionary<string, float> Initialize(string[] states, string currentState);

        // should be called each frame
        Dictionary<string, float> GetTransition(Dictionary<string, float> prevState, 
            string currentState, float stateChangeDeltaTime, out bool changed);
    }
}
