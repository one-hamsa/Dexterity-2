using System.Collections.Generic;
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
        }

        public float transitionSpeed = 10f;
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
            }
            return default;
        }
    }
}
