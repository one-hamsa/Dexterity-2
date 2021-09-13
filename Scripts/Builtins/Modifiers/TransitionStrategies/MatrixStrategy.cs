using System.Collections.Generic;
using UnityEngine;
using System;
using OneHumus.Data;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class MatrixStrategy : BaseStrategy
    {
        [Serializable]
        public class MatrixStrategyRow
        {
            [State]
            public string from;
            [State]
            public string to;

            public float time;

            [NonSerialized]
            public int fromId, toId;
        }

        public enum EasingStyle
        {
            EaseInOut,
            Linear,
        }

        public MatrixStrategyRow[] transitions;
        public EasingStyle easing;
        private float estimatedTime;
        private AnimationCurve easingCurve;
        protected override bool checkActivityThreshold => false;
        private IDictionary<int, float> actualValues = new ListMap<int, float>();

        public override IDictionary<int, float> Initialize(int[] states, int currentState)
        {
            foreach (var transition in transitions)
            {
                transition.fromId = Manager.instance.GetStateID(transition.from);
                transition.toId = Manager.instance.GetStateID(transition.to);
            }

            switch (easing)
            {
                case EasingStyle.EaseInOut:
                    easingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
                    break;
                case EasingStyle.Linear:
                    easingCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
                    break;
            }

            foreach (var state in states)
            {
                actualValues[state] = state == currentState ? 1 : 0;
            }

            return base.Initialize(states, currentState);
        }

        public override IDictionary<int, float> GetTransition(IDictionary<int, float> prevState,
            int currentState, float stateChangeDeltaTime, out bool changed)
        {
            estimatedTime = 0f;
            foreach (var kv in prevState)
            {
                var state = kv.Key;
                var value = actualValues[state];
                foreach (var transition in transitions)
                {
                    if (transition.fromId == state && transition.toId == currentState)
                    {
                        estimatedTime += Mathf.Lerp(0, 
                            Mathf.Max(0, transition.time - stateChangeDeltaTime), 
                            state == currentState ? 1 - value : value);
                    }
                }
            }

            return base.GetTransition(prevState, currentState, stateChangeDeltaTime, out changed);
        }

        protected override float GetStateValue(int state, int currentState, float currentValue)
        {
            
            var newValue = Mathf.Lerp(actualValues[state], state == currentState ? 1 : 0, 
                Time.deltaTime / estimatedTime);
            actualValues[state] = newValue;

            return easingCurve.Evaluate(newValue);
        }
    }
}
