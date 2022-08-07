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
                modifier.Awake();
        }
    }
}