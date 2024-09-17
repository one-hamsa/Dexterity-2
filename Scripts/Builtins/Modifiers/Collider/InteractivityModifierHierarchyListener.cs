using System;
using UnityEngine;

namespace OneHamsa.Dexterity.Builtins
{
    public class InteractivityModifierHierarchyListener : MonoBehaviour
    {
        public InteractivityModifier interactivityModifier;

        private void Awake()
        {
            if (interactivityModifier == null)
                interactivityModifier = GetComponentInParent<InteractivityModifier>();
        }

        private void OnEnable()
        {
            if (interactivityModifier == null)
            {
                enabled = false;
                return;
            }
            
            interactivityModifier.SetDirty();
        }

        private void OnDisable()
        {
            if (interactivityModifier == null)
                return;
            
            interactivityModifier.SetDirty();
        }

        private void OnTransformChildrenChanged()
        {
            interactivityModifier.SetDirty();
        }
    }
}