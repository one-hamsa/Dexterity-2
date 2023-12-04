using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Builtins
{
    [Serializable]
    public abstract class BaseStrategy : ITransitionStrategy
    {
        protected const float activityThreshold = 0.999f;

        protected InsertSortList<float> result = new();
        protected InsertSortList<float> nextResult = new();
        protected int[] states;

        private bool jumpedToFinalState;

        public virtual InsertSortList<float> Initialize(int[] states, int currentState)
        {
            this.states = states;
            
            jumpedToFinalState = false;

            result.Clear();
            nextResult.Clear();
            var foundState = false;
            foreach (var state in states)
            {
                result[state] = state == currentState ? 1 : 0;
                foundState |= state == currentState;
            }

            if (!foundState)
                throw new ITransitionStrategy.TransitionInitializationException($"did not find state " +
                    $"{currentState} ({Database.instance.GetStateAsString(currentState)}) in states");
            
            return result;
        }
        public virtual InsertSortList<float> GetTransition(InsertSortList<float> prevState, 
            int currentState, double timeSinceStateChange, double deltaTime, out bool changed)
        {
            changed = false;
            prevState.TryGetValue(currentState, out var prevValue);
            if (prevValue > activityThreshold)
            {
                changed = !jumpedToFinalState;
                jumpedToFinalState = true;

                if (changed)
                {
                    // jump to state goal
                    foreach (var state in states)
                        nextResult[state] = state == currentState ? 1 : 0;

                    // swap pointers
                    (result, nextResult) = (nextResult, result);
                }

                return result;
            }

            jumpedToFinalState = false;
            changed = true;

            // write to new pointer
            nextResult.Clear();
            var total = 0f;
            foreach (var kv in prevState.keyValuePairs)
            {
                var state = kv.Key;
                var value = kv.Value;

                float current;
                nextResult[state] = current = GetStateValue(state, currentState, value, deltaTime);
                total += current;
            }
            // normalize (in case numbers != 1)
            foreach (var state in states)
                nextResult[state] = Mathf.InverseLerp(0, total, nextResult[state]);

            // swap pointers
            (result, nextResult) = (nextResult, result);

            return result;
        }

        /**
         * naming here can get confusing:
         * state - the state for which we want to get the value
         * currentState - the current active state (target state)
         * currentValue - the value of state prior to this update
         */
        protected virtual float GetStateValue(int state, int currentState, float currentValue, double deltaTime)
        {
            return state == currentState ? 1 : 0;
        }
    }
}
