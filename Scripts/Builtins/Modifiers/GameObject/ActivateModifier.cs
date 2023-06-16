using UnityEngine;
using System;
using System.Collections;

namespace OneHamsa.Dexterity.Builtins
{
    public class ActivateModifier : Modifier
    {
        private Coroutine coro;
        public override bool animatableInEditor => false;

        [Serializable]
        public class Property : PropertyBase
        {
            // custom params
            public bool active;
        }


        public override void Refresh()
        {
            // don't do anything here, we'll run update loop as a coroutine on node (to keep alive when inactive)
        }

        IEnumerator UpdateAlwaysCoro()
        {
            while (true)
            {
                if (this != null && GetNode() != null && GetNode().isActiveAndEnabled && enabled)
                {
                    base.Refresh();

                    if (transitionChanged)
                        gameObject.SetActive(((Property)GetProperty(GetNode().GetActiveState())).active);
                }

                yield return null;
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

        public void OnDestroy()
        {
            // cleanup now
            base.OnDisable();
            
            if (coro != null && Manager.instance != null)
                Manager.instance.StopCoroutine(coro);
        }
    }
}
