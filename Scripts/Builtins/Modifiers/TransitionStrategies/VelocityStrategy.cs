using System.Collections.Generic;
using UnityEngine;
using System;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class VelocityStrategy : BaseStrategy
    {
        public float smoothTime = 1.5f;
        public float maxSpeed = Mathf.Infinity;

        private float currentVelocity = default;

        protected override float GetStateValue(int state, int currentState, float currentValue, double deltaTime)
        {
            var targetValue = state == currentState ? 1 : 0;
            return Mathf.SmoothDamp(currentValue, targetValue, ref currentVelocity, (float)(smoothTime * deltaTime), maxSpeed);
        }
    }
}
