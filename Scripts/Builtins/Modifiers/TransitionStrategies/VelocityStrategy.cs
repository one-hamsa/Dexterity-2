using System.Collections.Generic;
using UnityEngine;
using System;
using OneHumus.Data;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class VelocityStrategy : BaseStrategy
    {
        public float smoothTime = .03f;
        public float maxSpeed = Mathf.Infinity;

        private float currentVelocity = default;

        protected override float GetStateValue(int state, int currentState, float currentValue)
        {
            var targetValue = state == currentState ? 1 : 0;
            return Mathf.SmoothDamp(currentValue, targetValue, ref currentVelocity, smoothTime, maxSpeed);
        }
    }
}
