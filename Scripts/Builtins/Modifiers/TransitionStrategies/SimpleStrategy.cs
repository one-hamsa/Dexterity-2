using System.Collections.Generic;
using UnityEngine;
using System;
using OneHumus.Data;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class SimpleStrategy : ITransitionStrategy
    {
        public enum TransitionStyle
        {
            ContinuousLerp,
            Discrete,
        }

        public float transitionSpeed = 10f;
        public TransitionStyle style = TransitionStyle.ContinuousLerp;

        private float activityThreshold;

        IDictionary<int, float> result = new ListMap<int, float>();
        IDictionary<int, float> nextResult = new ListMap<int, float>();
        public IDictionary<int, float> Initialize(int[] states, int currentState) 
        {
            activityThreshold = Manager.instance.settings.GetGlobalFloat("activityThreshold", .999f);

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
