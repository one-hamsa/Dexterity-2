using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    public class ModifierDelayer : MonoBehaviour
    {
        public List<Modifier.TransitionDelay> delays = new();
        
        private void OnEnable()
        {
            foreach (var modifier in GetComponentsInChildren<Modifier>())
            {
                foreach (var delay in delays)
                    modifier.AddDelay(delay);
            }
        }
    }
}