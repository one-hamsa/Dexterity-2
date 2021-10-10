using OneHumus.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    [DefaultExecutionOrder(Manager.modifierExecutionPriority)]
    public class TransitionInterpolator : TransitionBehaviour
    {
        private int[] _states = new int[2] { 0, 1 };
        private float _stateChangeTime;

        protected override int[] states => _states;
        protected override float stateChangeTime => _stateChangeTime;
        protected override int activeState => 1;

        public float target { get; private set; }
        public float value => Mathf.Lerp(previousValue, target, transitionState[1]);
        private float previousValue;

        /// <summary>
        /// set a target between 0 and 1 to the modifier (interpolates the animation)
        /// </summary>
        /// <param name="target">normalized target</param>
        public void SetTarget(float target)
        {
            previousValue = value;

            target = Mathf.Clamp01(target);

            transitionState[0] = 1;
            transitionState[1] = 0;

            _stateChangeTime = Time.time;
            this.target = target;
        }
    }

}
