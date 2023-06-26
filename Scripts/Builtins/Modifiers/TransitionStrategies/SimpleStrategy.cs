using UnityEngine;
using System;

namespace OneHamsa.Dexterity.Visual.Builtins
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
        
        public override ITransitionStrategy Clone()
        {
            var clone = new SimpleStrategy
            {
                transitionSpeed = transitionSpeed,
                transitionTime = transitionTime,
                style = style
            };
            return clone;
        }
    }
}
