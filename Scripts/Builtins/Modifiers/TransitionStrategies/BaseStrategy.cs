using System.Collections.Generic;
using UnityEngine;
using System;
using OneHumus.Data;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public abstract class BaseStrategy : ITransitionStrategy
    {
        protected virtual bool checkActivityThreshold => true;
        protected float activityThreshold;

        protected IDictionary<int, float> result = new ListMap<int, float>();
        protected IDictionary<int, float> nextResult = new ListMap<int, float>();
        public virtual IDictionary<int, float> Initialize(int[] states, int currentState) 
        {
            activityThreshold = Manager.instance.settings.GetGlobalFloat("activityThreshold", .999f);

            result.Clear();
            foreach (var state in states)
            {
                result[state] = state == currentState ? 1 : 0;
            }
            
            return result;
        }
        public virtual IDictionary<int, float> GetTransition(IDictionary<int, float> prevState, 
            int currentState, float stateChangeDeltaTime, out bool changed)
        {
            changed = false;
            if (checkActivityThreshold && prevState[currentState] > activityThreshold)
            {
                // jump to final state
                foreach (var kv in prevState)
                {
                    var state = kv.Key;

                    if (state == currentState)
                        prevState[state] = 1;
                    else
                        prevState[state] = 0;
                }

                return prevState;
            }

            changed = true;
            // write to new pointer
            nextResult.Clear();
            var total = 0f;
            foreach (var kv in prevState)
            {
                var state = kv.Key;
                var value = kv.Value;

                float current;
                nextResult[state] = current = GetStateValue(state, currentState, value);
                total += current;
            }
            // normalize (in case numbers != 1)
            foreach (var kv in nextResult)
            {
                nextResult[kv.Key] = kv.Value / total;
            }

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
        protected virtual float GetStateValue(int state, int currentState, float currentValue)
        {
            return state == currentState ? 1 : 0;
        }
    }
}
