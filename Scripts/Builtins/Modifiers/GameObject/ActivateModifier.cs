using UnityEngine;
using System;
using System.Collections;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class ActivateModifier : Modifier
    {
        private static WaitForEndOfFrame waitForEndOfFrame = new();
        private Coroutine coro;
        public override bool animatableInEditor => false;

        [Serializable]
        public class Property : PropertyBase
        {
            // custom params
            public bool active;
        }


        public override void Update()
        {
            // don't do anything here, we'll run update loop as a coroutine on node (to keep alive when inactive)
        }

        IEnumerator UpdateAlwaysCoro()
        {
            while (true)
            {
                if (node.isActiveAndEnabled && enabled)
                {
                    base.Update();

                    if (transitionChanged)
                        gameObject.SetActive(((Property)GetProperty(node.activeState)).active);
                }

                yield return waitForEndOfFrame;
            }
        }

        public override void Awake()
        {
            base.Awake();
            
            if (enabled && !gameObject.activeInHierarchy)
                // enable anyway (so that coroutine can be started)
                base.OnEnable();
        }

        public override void HandleNodeEnabled()
        {
            base.HandleNodeEnabled();
            coro ??= Manager.instance.StartCoroutine(UpdateAlwaysCoro());
        }

        protected override void OnDisable()
        {
            // actually, don't cleanup just yet
        }

        public override void OnDestroy()
        {
            // cleanup now
            base.OnDisable();
            
            if (coro != null)
                StopCoroutine(coro);
            
            base.OnDestroy();
        }
    }
}
