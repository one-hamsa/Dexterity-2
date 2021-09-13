using System.Collections.Generic;
using UnityEngine;
using System;
using OneHumus.Data;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class SimpleStrategy : BaseStrategy
    {
        public enum TransitionStyle
        {
            ContinuousLerp,
            Discrete,
        }

        public float transitionSpeed = 10f;
        public TransitionStyle style = TransitionStyle.ContinuousLerp;

        protected override float GetStateValue(int state, int currentState, float currentValue)
        {
            var targetValue = state == currentState ? 1 : 0;

            switch (style)
            {
                case TransitionStyle.ContinuousLerp:
                    return Mathf.Lerp(currentValue, targetValue, transitionSpeed * Time.deltaTime);
                case TransitionStyle.Discrete:
                    return targetValue;
            }
            return default;
        }
    }
}
