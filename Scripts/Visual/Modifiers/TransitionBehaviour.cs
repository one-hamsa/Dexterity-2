using System;
using System.Collections.Generic;
using System.Linq;
using OneHamsa.Dexterity.Visual.Utilities;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    public abstract class TransitionBehaviour : MonoBehaviour
    {
        [SerializeReference]
        public ITransitionStrategy transitionStrategy;

        protected bool transitionChanged;
        protected int forceTransitionChangeFrames;

        protected ListDictionary<int, float> transitionState;
        private double timeSinceUpdate;

        protected abstract double deltaTime { get; }
        public abstract int GetActiveState();
        protected abstract double timeSinceStateChange { get; }
        protected abstract int[] states { get; }

        public virtual bool IsChanged() => transitionChanged;

        public event Action<int> onTransitionStarted;
        public event Action<int> onTransitionEnded;

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

        public virtual void Refresh()
        {
            var prevTransitionChanged = transitionChanged;

            transitionState = transitionStrategy.GetTransition(transitionState,
                GetActiveState(), timeSinceStateChange, deltaTime, out transitionChanged);

            if (forceTransitionChangeFrames > 0)
            {
                forceTransitionChangeFrames--;
                transitionChanged = true;
            }

            if (transitionChanged && !prevTransitionChanged) {
                onTransitionStarted?.Invoke(GetActiveState());
            } else if (!transitionChanged && prevTransitionChanged) {
                onTransitionEnded?.Invoke(GetActiveState());
            }
        }

        public void InitializeTransitionState()
        {
            // create default if doesn't exist
            transitionStrategy ??= Database.instance.settings.CreateDefaultTransitionStrategy();

            try {
                transitionState = transitionStrategy.Initialize(states, GetActiveState());
            } catch (ITransitionStrategy.TransitionInitializationException e) {
                Debug.LogException(e, this);
                if (Application.isPlaying)
                    enabled = false;
            }
        }

        /// <summary>
        /// Force updating this modifier's transition (even if the transition function reports it's not needed)
        /// </summary>
        /// <param name="frames">How many frames should the update be forced for</param>
        public void ForceTransitionUpdate(int frames = 1)
        {
            forceTransitionChangeFrames = frames;
            Refresh();
        }
    }
}
