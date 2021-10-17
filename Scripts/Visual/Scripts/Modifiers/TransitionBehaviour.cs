using OneHumus.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    public abstract class TransitionBehaviour : MonoBehaviour
    {
        [SerializeReference]
        public ITransitionStrategy transitionStrategy;

        protected bool transitionChanged;
        protected int forceTransitionChangeFrames;

        protected IDictionary<int, float> transitionState;

        protected abstract int activeState { get; }
        protected abstract float stateChangeTime { get; }
        protected abstract int[] states { get; }

        protected virtual void Start()
        {
            transitionState = transitionStrategy.Initialize(states, activeState);
        }
        protected virtual void OnEnable()
        {
            ForceTransitionUpdate();
        }
        protected virtual void OnDisable()
        {

        }

        protected virtual void Update()
        {
            transitionState = transitionStrategy.GetTransition(transitionState,
                activeState, Time.time - stateChangeTime, out transitionChanged);

            if (forceTransitionChangeFrames > 0)
            {
                forceTransitionChangeFrames--;
                transitionChanged = true;
            }
        }

        /// <summary>
        /// Force updating this modifier's transition (even if the transition function reports it's not needed)
        /// </summary>
        /// <param name="frames">How many frames should the update be forced for</param>
        public void ForceTransitionUpdate(int frames = 1) => forceTransitionChangeFrames = frames;
    }

}
