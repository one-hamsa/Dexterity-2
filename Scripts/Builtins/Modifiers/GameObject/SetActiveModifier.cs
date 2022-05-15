using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Collections;
using UnityEngine.UI;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class SetActiveModifier : Modifier
    {
        private static WaitForEndOfFrame waitForEndOfFrame = new WaitForEndOfFrame();
        
        [Serializable]
        public class Property : PropertyBase
        {
            // custom params
            public bool active;
        }


        public override void Update()
        {
            // don't do anything here, we'll run update loop as a coroutine on node
        }

        IEnumerator UpdateAlways()
        {
            while (true)
            {
                base.Update();

                if (transitionChanged)
                {
                    var normalizedSum = 0f;
                    foreach (var kv in transitionState)
                    {
                        var property = GetProperty(kv.Key) as Property;
                        var value = kv.Value;

                        normalizedSum += property.active ? value : 0f;
                    }

                    gameObject.SetActive(normalizedSum > 0f);
                }

                yield return waitForEndOfFrame;
            }
        }

        public override void Awake()
        {
            base.Awake();
            
            // don't run on edit time - this might be triggered by an animation
            if (Application.isPlaying)
                // enable anyway, because we might get disabled
                base.OnEnable();
        }

        public override void HandleNodeEnabled()
        {
            base.HandleNodeEnabled();
            node.StartCoroutine(UpdateAlways());
        }

        protected override void OnDisable()
        {
            // actually, don't cleanup just yet
        }

        public override void OnDestroy()
        {
            // cleanup now
            base.OnDisable();
            
            base.OnDestroy();
        }
    }
}
