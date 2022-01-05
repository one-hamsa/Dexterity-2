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
        private double lastUpdateTime;

        protected abstract double currentTime { get; }
        protected abstract int activeState { get; }
        protected abstract double stateChangeTime { get; }
        protected abstract int[] states { get; }

        public bool IsChanged() => transitionChanged;

        public virtual void Awake()
        {
            InitializeTransitionState();
        }
        protected virtual void OnEnable()
        {
            ForceTransitionUpdate();
        }
        protected virtual void OnDisable()
        {

        }

        public virtual void Update()
        {
            transitionState = transitionStrategy.GetTransition(transitionState,
                activeState, currentTime - stateChangeTime, currentTime - lastUpdateTime, out transitionChanged);

            lastUpdateTime = currentTime;

            if (forceTransitionChangeFrames > 0)
            {
                forceTransitionChangeFrames--;
                transitionChanged = true;
            }
        }

        public void InitializeTransitionState()
        {
            transitionState = transitionStrategy.Initialize(states, activeState);
            lastUpdateTime = currentTime;
        }

        /// <summary>
        /// Force updating this modifier's transition (even if the transition function reports it's not needed)
        /// </summary>
        /// <param name="frames">How many frames should the update be forced for</param>
        public void ForceTransitionUpdate(int frames = 1) => forceTransitionChangeFrames = frames;
    }

}
