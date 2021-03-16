using System.Collections.Generic;
using UnityEngine;
using System;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class SimpleStrategy : ITransitionStrategy
    {
        // TODO custom attribute drawer
        [Serializable]
        public class TransitionDelay
        {
            public string State;
            public float Delay = 0;
            public float PreviousStateThreshold = .95f;
        } 

        public enum TransitionStyle
        {
            ContinuousLerp,
            Discrete,
        }

        public float TransitionSpeed = 10f;
        public TransitionStyle Style = TransitionStyle.ContinuousLerp;
        public float ActivityThreshold = .999f;
        public List<TransitionDelay> Delays;

        TransitionDelay GetDelay(string state)
        {
            foreach (var d in Delays)
                if (d.State == state)
                    return d;

            return null;
        }

        Dictionary<string, float> result = new Dictionary<string, float>();
        Dictionary<string, float> nextResult = new Dictionary<string, float>();
        public Dictionary<string, float> Initialize(string[] states, string currentState) 
        {
            result.Clear();
            foreach (var state in states)
            {
                result[state] = state == currentState ? 1 : 0;
            }
            
            return result;
        }
        public Dictionary<string, float> GetTransition(Dictionary<string, float> prevState, 
            string currentState, float stateChangeDeltaTime, out bool changed)
        {
            changed = false;
            if (prevState[currentState] > ActivityThreshold)
                return prevState;

            var delay = GetDelay(currentState);
            if (delay != null && stateChangeDeltaTime < delay.Delay)
            {
                foreach (var kv in prevState)
                {
                    if (kv.Key != currentState && kv.Value >= delay.PreviousStateThreshold)
                        return prevState;
                }
            }

            changed = true;
            // write to new pointer
            nextResult.Clear();
            foreach (var kv in prevState)
            {
                var state = kv.Key;
                var value = kv.Value;

                switch (Style)
                {
                    case TransitionStyle.ContinuousLerp:
                        nextResult[state] = Mathf.Lerp(value, state == currentState ? 1 : 0, TransitionSpeed * Time.deltaTime);
                        break;
                    case TransitionStyle.Discrete:
                        nextResult[state] = state == currentState ? 1 : 0;
                        break;
                }
            }
            // swap pointers
            (result, nextResult) = (nextResult, result);

            return result;
        }
    }
}
