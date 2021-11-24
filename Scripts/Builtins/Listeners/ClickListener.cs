using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class ClickListener : MonoBehaviour
    {
        [SerializeField]
        protected Node node;

        [SerializeField]
        public UnityEvent onClick;

        Node.OutputField pressedField;
        Node.OutputField hoverField;
        Node.OutputField disabledField;

        void Awake()
        {
            if (!node)
                node = GetComponentInParent<Node>();

            if (!node)
            {
                Debug.LogWarning($"Node not found for listener ({gameObject.name})");
                enabled = false;
                return;
            }

        }

        void OnEnable()
        {
            pressedField = node.GetOutputField("pressed");
            hoverField = node.GetOutputField("hover");
            disabledField = node.GetOutputField("disabled");

            pressedField.onBooleanValueChanged += HandlePress;
        }
        void OnDisable()
        {
            pressedField.onBooleanValueChanged -= HandlePress;
        }

        private void HandlePress(Node.OutputField field, bool oldValue, bool newValue)
        {
            // don't handle if disabled
            if (disabledField.GetBooleanValue())
                return;

            // only handle if unpressed while on object
            if (oldValue && !newValue && hoverField.GetBooleanValue())
            {
                Click();
            }
        }

        public void Click() => onClick?.Invoke();
    }
}
