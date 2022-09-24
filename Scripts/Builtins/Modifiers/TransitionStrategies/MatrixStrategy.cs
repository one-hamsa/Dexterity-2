using System.Collections.Generic;
using UnityEngine;
using System;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    [Serializable, RequiresStateFunction]
    public class MatrixStrategy : BaseStrategy
    {
        public MatrixDefinition definition;

        private int lastState = -1;
        private MatrixDefinition.Row lastMatrixRow;
        private AnimationCurve lastEasingCurve;
        private float lastTotalTransitionTime;
        private double timeSinceRowChange;
        protected override bool checkActivityThreshold => false;
        private IDictionary<int, float> transitionStartValues = new Dictionary<int, float>();

        public override IDictionary<int, float> Initialize(int[] states, int currentState)
        {
            definition.Initialize();
            var initialRow = definition.GetRow(lastState, currentState);
            lastEasingCurve = initialRow.easingCurve;
            lastTotalTransitionTime = initialRow.time;
            lastState = currentState;

            foreach (var state in states)
            {
                transitionStartValues[state] = state == currentState ? 1 : 0;
            }

            return base.Initialize(states, currentState);
        }

        public override IDictionary<int, float> GetTransition(IDictionary<int, float> prevState, 
            int currentState, double timeSinceStateChange, double deltaTime, out bool changed)
        {
            // manually track state changes
            if (currentState != lastState)
            {
                var newRow = definition.GetRow(lastState, currentState);
                // compare to matrix "row" - if it's the same row, don't restart transition
                if (newRow != lastMatrixRow) {
                    // manually save time since transition started
                    timeSinceRowChange = timeSinceStateChange;

                    // update values according to new row
                    lastEasingCurve = newRow.easingCurve;
                    var time = newRow.time;
                    // calculate how much time needed for this row's transition, maybe we're already in the middle of it
                    var timeDiff = Mathf.Max(0, (float)(time - timeSinceStateChange));
                    lastTotalTransitionTime = timeDiff * (1 - prevState[currentState]);

                    // save start values for all states to allow transitioning in the middle
                    foreach (var kv in prevState)
                    {
                        transitionStartValues[kv.Key] = kv.Value;
                    }
                }

                lastMatrixRow = newRow;
                lastState = currentState;
            }
            // manually progress clock
            timeSinceRowChange += deltaTime;

            // note: send lastState and timeSinceRowChange instead of currentState and timeSinceStateChange
            return base.GetTransition(prevState, lastState, timeSinceRowChange, deltaTime, out changed);
        }

        protected override float GetStateValue(int state, int currentState, float currentValue, double deltaTime)
        {
            // calculate the fraction of time elapsed since the row had changed (use InverseLerp to avoid NaNs)
            var timeFraction = Mathf.InverseLerp(0, lastTotalTransitionTime, (float)timeSinceRowChange);
            // now lerp between the start value and 0/1 depending on the easing curve
            return Mathf.Lerp(transitionStartValues[state], 
                state == currentState ? 1 : 0, 
                lastEasingCurve.Evaluate(timeFraction));
        }
    }
}
