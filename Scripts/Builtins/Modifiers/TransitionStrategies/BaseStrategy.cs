using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    [Serializable]
    public abstract class BaseStrategy : ITransitionStrategy
    {
        protected virtual bool checkActivityThreshold => true;
        protected float activityThreshold;

        protected IDictionary<int, float> result = new Dictionary<int, float>();
        protected IDictionary<int, float> nextResult = new Dictionary<int, float>();

        // before .NET 5.0, modifications will break the dictionary enumerator, so this hack is required.
        // see https://github.com/dotnet/runtime/pull/34667
        private List<(int key, float value)> changeList = new List<(int key, float value)>();

        private bool jumpedToFinalState;

        public virtual IDictionary<int, float> Initialize(int[] states, int currentState) 
        {
            activityThreshold = Core.instance.settings.GetGlobalFloat("activityThreshold", .999f);
            jumpedToFinalState = false;

            result.Clear();
            var foundState = false;
            foreach (var state in states)
            {
                result[state] = state == currentState ? 1 : 0;
                foundState |= state == currentState;
            }

            if (!foundState)
                Debug.LogError($"BaseStrategy.Initialize: did not find state {currentState} in states");
            
            return result;
        }
        public virtual IDictionary<int, float> GetTransition(IDictionary<int, float> prevState, 
            int currentState, double timeSinceStateChange, double deltaTime, out bool changed)
        {
            changed = false;
            prevState.TryGetValue(currentState, out var prevValue);
            if (checkActivityThreshold && prevValue > activityThreshold)
            {
                // jump to final state
                changed = !jumpedToFinalState;
                jumpedToFinalState = true;

                changeList.Clear();
                foreach (var state in prevState.Keys) {
                    if (state == currentState)
                        changeList.Add((state, 1));
                    else
                        changeList.Add((state, 0));
                }
                foreach (var (key, value) in changeList)
                    nextResult[key] = value;

                // swap pointers
                (result, nextResult) = (nextResult, result);

                return result;
            }

            jumpedToFinalState = false;
            changed = true;

            // write to new pointer
            nextResult.Clear();
            var total = 0f;
            foreach (var kv in prevState)
            {
                var state = kv.Key;
                var value = kv.Value;

                float current;
                nextResult[state] = current = GetStateValue(state, currentState, value, deltaTime);
                total += current;
            }
            // normalize (in case numbers != 1)
            changeList.Clear();
            foreach (var kv in nextResult)
                changeList.Add((kv.Key, Mathf.InverseLerp(0, total, kv.Value)));
            foreach (var (key, value) in changeList)
                    nextResult[key] = value;

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
