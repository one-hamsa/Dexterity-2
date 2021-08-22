using System.Collections.Generic;
using UnityEngine;
using System;
using OneHumus.Data;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class SimpleStrategy : ITransitionStrategy
    {
        // TODO custom attribute drawer
        [Serializable]
        public class TransitionDelay
        {
            public string state;
            public float delay = 0;
            public float previousStateThreshold = .95f;
        } 

        public enum TransitionStyle
        {
            ContinuousLerp,
            Discrete,
        }

        public float transitionSpeed = 10f;
        public TransitionStyle style = TransitionStyle.ContinuousLerp;
        public float activityThreshold = .999f;
        public List<TransitionDelay> delays;

        ListMap<int, TransitionDelay> cachedDelays;

        IDictionary<int, float> result = new ListMap<int, float>();
        IDictionary<int, float> nextResult = new ListMap<int, float>();
        public IDictionary<int, float> Initialize(int[] states, int currentState) 
        {
            // cache delays
            cachedDelays = new ListMap<int, TransitionDelay>();
            foreach (var delay in delays)
                cachedDelays.Add(Manager.instance.GetStateID(delay.state), delay);

            result.Clear();
            foreach (var state in states)
            {
                result[state] = state == currentState ? 1 : 0;
            }
            
            return result;
        }
        public IDictionary<int, float> GetTransition(IDictionary<int, float> prevState, 
            int currentState, float stateChangeDeltaTime, out bool changed)
        {
            changed = false;
            if (prevState[currentState] > activityThreshold)
                return prevState;

            cachedDelays.TryGetValue(currentState, out var delay);
            if (delay != null && stateChangeDeltaTime < delay.delay)
            {
                foreach (var kv in prevState)
                {
                    if (kv.Key != currentState && kv.Value >= delay.previousStateThreshold)
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

                switch (style)
                {
                    case TransitionStyle.ContinuousLerp:
                        nextResult[state] = Mathf.Lerp(value, state == currentState ? 1 : 0, transitionSpeed * Time.deltaTime);
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
