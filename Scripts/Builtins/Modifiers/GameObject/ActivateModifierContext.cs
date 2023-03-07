using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class ActivateModifierContext : MonoBehaviour
    {
        private ActivateModifier[] activateModifers;

        private void OnEnable()
        {
            activateModifers = GetComponentsInChildren<ActivateModifier>(true);

            foreach (var modifier in activateModifers)
            {
                if (!modifier.enabled)
                    continue;

                if (!modifier.gameObject.activeInHierarchy)
                {
                    // just make sure the modifier finds its node before we reparent and enable it
                    modifier._node = modifier.GetNode();
                    ForceAwake(modifier.gameObject);
                }
            }
        }

        private void ForceAwake(GameObject gameObject)
        {
            // save all transform data that's going to be changed
            var parent = gameObject.transform.parent;
            var activeSelf = gameObject.activeSelf;
            var localPosition = gameObject.transform.localPosition;
            var localRotation = gameObject.transform.localRotation;
            var localScale = gameObject.transform.localScale;
            var siblingIndex = gameObject.transform.GetSiblingIndex();
            
            // set parent to root
            gameObject.transform.SetParent(transform);
            // make sure it's enabled
            gameObject.SetActive(true);
            
            // set transform data back to what it was
            gameObject.SetActive(activeSelf);
            gameObject.transform.SetParent(parent);
            gameObject.transform.SetSiblingIndex(siblingIndex);
            gameObject.transform.localPosition = localPosition;
            gameObject.transform.localRotation = localRotation;
            gameObject.transform.localScale = localScale;
        }
    }
}