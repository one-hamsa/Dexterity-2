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
        private double _timeSinceStateChange;

        // time scale won't affect this
        protected override double deltaTime => Core.instance.deltaTime;
        protected override double timeSinceStateChange => _timeSinceStateChange;
        
        protected override int[] states => _states;
        public override int activeState => 1;

        public float target { get; private set; }
        public float value => Mathf.Lerp(previousValue, target, transitionState[1]);
        private float previousValue;

        /// <summary>
        /// set a target between 0 and 1 to the modifier (interpolates the animation)
        /// </summary>
        /// <param name="target">normalized target</param>
        public void SetTarget(float target)
        {
            if (float.IsNaN(target))
                return;
                
            previousValue = value;
            Restart();

            _timeSinceStateChange = 0f;
            this.target = Mathf.Clamp01(target);
        }

        /// <summary>
        /// plays transition (makes it run between 0 and 1)
        /// </summary>
        public void Play() 
        {
            Restart();
            previousValue = 0;
            SetTarget(1f);
        }

        /// <summary>
        /// restarts transition momentum
        /// </summary>
        public void Restart()
        {
            transitionState[0] = 1;
            transitionState[1] = 0;
        }

        public override void Update()
        {
            base.Update();
            _timeSinceStateChange += deltaTime;
        }
    }

}
