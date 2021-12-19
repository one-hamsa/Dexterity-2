using System.Collections.Generic;
using UnityEngine;
using System;
using OneHumus.Data;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    [RequiresStateFunction]
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

        [Serializable]
        public class MatrixStrategyData
        {
            public MatrixStrategyRow[] rows;

            public void Initialize()
            {
                foreach (var transition in rows)
                {
                    transition.fromId = Manager.instance.GetStateID(transition.from);
                    transition.toId = Manager.instance.GetStateID(transition.to);
                }
            }

            public float GetTime(string fromState, string toState)
            {
                foreach (var transition in rows)
                {
                    if (transition.from == fromState && transition.to == toState)
                        return transition.time;
                }
                return default;
            }

            public float GetTime(int fromState, int toState)
            {
                foreach (var transition in rows)
                {
                    if (transition.fromId == fromState && transition.toId == toState)
                        return transition.time;
                }
                return default;
            }

            public void SetTime(string fromState, string toState, float time)
            {
                foreach (var transition in rows)
                {
                    if (transition.from == fromState && transition.to == toState)
                    {
                        transition.time = time;
                        break;
                    }
                }
            }
        }

        public enum EasingStyle
        {
            EaseInOut,
            Linear,
        }

        public MatrixStrategyData transitions;

        public EasingStyle easing;
        private float estimatedTime;
        private AnimationCurve easingCurve;
        protected override bool checkActivityThreshold => false;
        private IDictionary<int, float> actualValues = new ListMap<int, float>();

        public override IDictionary<int, float> Initialize(int[] states, int currentState)
        {
            transitions.Initialize();

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
            int currentState, double timeSinceStateChange, double deltaTime, out bool changed)
        {
            estimatedTime = 0f;
            foreach (var kv in prevState)
            {
                var state = kv.Key;
                var value = actualValues[state];
                var time = transitions.GetTime(state, currentState);
                
                estimatedTime += Mathf.Lerp(0, 
                    Mathf.Max(0, (float)(time - timeSinceStateChange)),
                    state == currentState ? 1 - value : value);
            }

            return base.GetTransition(prevState, currentState, timeSinceStateChange, deltaTime, out changed);
        }

        protected override float GetStateValue(int state, int currentState, float currentValue, double deltaTime)
        {
            var newValue = Mathf.Lerp(actualValues[state], state == currentState ? 1 : 0, 
                (float)(deltaTime / estimatedTime));
            actualValues[state] = newValue;

            return easingCurve.Evaluate(newValue);
        }
    }
}
