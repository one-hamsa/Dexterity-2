using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    [RequireComponent(typeof(ClickListener))]
    public class ToggleListener : MonoBehaviour
    {
        public bool toggled;
        
        private ClickListener clickListener;

        private void Awake()
        {
            clickListener = GetComponent<ClickListener>();
        }

        private void OnEnable()
        {         
            clickListener.onClick.AddListener(OnClick);
        }
        
        private void OnDisable()
        {
            clickListener.onClick.RemoveListener(OnClick);
        }

        private void OnClick()
        {
            toggled = !toggled;
        }
    }
}
