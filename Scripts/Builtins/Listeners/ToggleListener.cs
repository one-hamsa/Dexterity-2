using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
    public class ToggleListener : MonoBehaviour
    {
        public bool toggled;
        public bool toggleOffWhenDisabled = false;
        public UnityEvent<bool> onToggle;
        public UnityEvent onToggleOn;
        public UnityEvent onToggleOff;

        private BaseClickListener clickListener;

        [Preserve]
        public bool IsToggled() => toggled;

        private void Awake()
        {
            clickListener = GetComponent<BaseClickListener>();
            if (clickListener == null)
            {
                Debug.LogError($"ToggleListener on {name} requires a sibling BaseClickListener " +
                               "(FieldNodeClickListener or GraphNodeClickListener).", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {         
            clickListener.onClick.AddListener(OnClick);
        }
        
        private void OnDisable()
        {
            clickListener.onClick.RemoveListener(OnClick);
            if (toggleOffWhenDisabled && toggled)
                ToggleOff();
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
