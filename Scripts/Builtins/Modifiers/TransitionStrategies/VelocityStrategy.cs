using System.Collections.Generic;
using UnityEngine;
using System;

namespace OneHamsa.Dexterity.Builtins
{
    [Serializable]
    public class VelocityStrategy : BaseStrategy
    {
        public float smoothTime = 0.167f;
        public float maxSpeed = Mathf.Infinity;

        private Dictionary<int, float> currentVelocities = new Dictionary<int, float>(); 

        public override Dictionary<int, float> Initialize(int[] states, int currentState)  
        {
            foreach (var state in states)
                currentVelocities[state] = 0f;

            return base.Initialize(states, currentState);
        }

        protected override float GetStateValue(int state, int currentState, float currentValue, double deltaTime)
        {
            var targetValue = state == currentState ? 1 : 0;
            var velocity = currentVelocities[state];
            var result = Mathf.SmoothDamp(currentValue, targetValue, ref velocity, (float)smoothTime, maxSpeed,
                (float)deltaTime);
            currentVelocities[state] = velocity;
            return result;
        }
    }
}
