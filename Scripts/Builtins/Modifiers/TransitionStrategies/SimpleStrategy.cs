using UnityEngine;
using System;

namespace OneHamsa.Dexterity.Builtins
{
    [Serializable]
    public class SimpleStrategy : BaseStrategy
    {
        public enum TransitionStyle
        {
            ContinuousLerp,
            Discrete,
            Linear
        }

        [Tooltip("For Continuous Lerp")]
        public float transitionSpeed = 10f;
        [Tooltip("For Linear")]
        public float transitionTime = 1f;
        public TransitionStyle style = TransitionStyle.ContinuousLerp;

        /// <summary>
        /// creates a new SimpleStrategy from an existing one
        /// </summary>
        public static SimpleStrategy CloneFrom(SimpleStrategy original)
        {
            return new SimpleStrategy
            {
                transitionSpeed = original.transitionSpeed,
                transitionTime = original.transitionTime,
                style = original.style
            };
        }

        protected override float GetStateValue(int state, int currentState, float currentValue, double deltaTime)
        {
            var targetValue = state == currentState ? 1 : 0;

            switch (style)
            {
                case TransitionStyle.ContinuousLerp:
                    return Mathf.Lerp(currentValue, targetValue, (float)(transitionSpeed * deltaTime));
                case TransitionStyle.Discrete:
                    return targetValue;
                case TransitionStyle.Linear:
                    float changeSign = targetValue > currentValue ? 1 : -1;
                    float timeLeft = (targetValue - currentValue) * changeSign * transitionTime;
                    if (deltaTime >= timeLeft)
                        return targetValue;
                    return currentValue + (float)deltaTime * changeSign / transitionTime;
            }
            return default;
        }
    }
}
