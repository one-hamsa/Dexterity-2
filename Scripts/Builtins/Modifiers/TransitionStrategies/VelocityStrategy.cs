using System.Collections.Generic;
using UnityEngine;
using System;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class VelocityStrategy : BaseStrategy
    {
        public float smoothTime = 0.167f;
        public float maxSpeed = Mathf.Infinity;

        private float[] currentVelocities; 

        public override IDictionary<int, float> Initialize(int[] states, int currentState)  
        {
            currentVelocities = new float[states.Length];
            return base.Initialize(states, currentState);
        }

        protected override float GetStateValue(int state, int currentState, float currentValue, double deltaTime)
        {
            var targetValue = state == currentState ? 1 : 0;
            return Mathf.SmoothDamp(currentValue, targetValue, ref currentVelocities[state], (float)smoothTime, maxSpeed,
                (float)deltaTime);
        }
    }
}
