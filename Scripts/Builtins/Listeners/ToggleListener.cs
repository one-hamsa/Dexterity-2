using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
    [RequireComponent(typeof(ClickListener))]
    public class ToggleListener : MonoBehaviour
    {
        public bool toggled;
        public UnityEvent<bool> onToggle;
        public UnityEvent onToggleOn;
        public UnityEvent onToggleOff;
        
        private ClickListener clickListener;
        
        [Preserve]
        public bool IsToggled() => toggled;

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

        private void OnClick() => Toggle();

        private void FireEvents()
        {
            onToggle.Invoke(toggled);
            if (toggled)
                onToggleOn.Invoke();
            else
                onToggleOff.Invoke();
        }

        public void Toggle()
        {
            toggled = !toggled;
            FireEvents();
        }

        public void Toggle(bool value) {
            toggled = value;
            FireEvents();
        }

        public void ToggleOn()
        {
            toggled = true;
            FireEvents();
        }

        public void ToggleOff()
        {
            toggled = false;
            FireEvents();
        }
    }
}
