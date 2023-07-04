using System.Collections.Generic;
using UnityEngine;
using System;

namespace OneHamsa.Dexterity.Builtins
{
    [Serializable, RequiresStateFunction]
    public class MatrixStrategy : BaseStrategy
    {
        public MatrixDefinition definition;

        private int trackedCurrentState = -1;
        private MatrixDefinition.Row currentMatrixRow;
        private AnimationCurve currentEasingCurve;
        private float currentTotalTransitionTime;
        private double timeSinceRowChange;
        private Dictionary<int, float> transitionStartValues = new();

        public override SortedList<int, float> Initialize(int[] states, int currentState)
        {
            definition.Initialize();
            var initialRow = definition.GetRow(trackedCurrentState, currentState);
            currentEasingCurve = initialRow.easingCurve;
            currentTotalTransitionTime = initialRow.time;
            trackedCurrentState = currentState;

            foreach (var state in states)
            {
                transitionStartValues[state] = state == currentState ? 1 : 0;
            }

            return base.Initialize(states, currentState);
        }

        public override SortedList<int, float> GetTransition(SortedList<int, float> prevState, 
            int currentState, double timeSinceStateChange, double deltaTime, out bool changed)
        {
            // manually track state changes
            if (currentState != trackedCurrentState 
                && (currentMatrixRow == null || timeSinceRowChange >= currentMatrixRow.minTimeBeforeExit))
            {
                var newRow = definition.GetRow(trackedCurrentState, currentState);
                // compare to matrix "row" - if it's the same row, don't restart transition
                if (newRow != currentMatrixRow) {
                    // manually save time since transition started
                    timeSinceRowChange = Mathf.Max((float)(timeSinceStateChange - currentMatrixRow?.minTimeBeforeExit ?? 0), 0f);

                    // update values according to new row
                    currentEasingCurve = newRow.easingCurve;
                    var time = newRow.time;
                    // calculate how much time needed for this row's transition, maybe we're already in the middle of it
                    var timeDiff = Mathf.Max(Mathf.Epsilon, (float)(time - timeSinceRowChange));
                    currentTotalTransitionTime = timeDiff * (1 - prevState[currentState]);

                    // save start values for all states to allow transitioning in the middle
                    foreach (var kv in prevState)
                    {
                        transitionStartValues[kv.Key] = kv.Value;
                    }
                }
                currentMatrixRow = newRow;
                trackedCurrentState = currentState;
            }
            // manually progress clock
            timeSinceRowChange += deltaTime;

            // note: send trackedCurrentState and timeSinceRowChange instead of currentState and timeSinceStateChange
            return base.GetTransition(prevState, trackedCurrentState, timeSinceRowChange, deltaTime, out changed);
        }

        protected override float GetStateValue(int state, int currentState, float currentValue, double deltaTime)
        {
            // calculate the fraction of time elapsed since the row had changed (use InverseLerp to avoid NaNs)
            var timeFraction = Mathf.InverseLerp(0, currentTotalTransitionTime, (float)timeSinceRowChange);
            // now lerp between the start value and 0/1 depending on the easing curve
            return Mathf.Lerp(transitionStartValues[state], 
                state == currentState ? 1 : 0, 
                currentEasingCurve.Evaluate(timeFraction));
        }
    }
}
