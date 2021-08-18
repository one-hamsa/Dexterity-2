using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class UIClickListener : MonoBehaviour
    {
        [SerializeField]
        protected Node node;

        [SerializeField]
        protected UnityEvent onClick;

        Node.OutputField pressedField;
        Node.OutputField hoverField;

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

            pressedField = node.GetOutputField("pressed");
            hoverField = node.GetOutputField("hover");

        }

        void OnEnable()
        {
            pressedField.onValueChanged += HandlePress;
        }
        void OnDisable()
        {
            pressedField.onValueChanged -= HandlePress;
        }

        private void HandlePress(Node.OutputField field, int oldValue, int newValue)
        {
            // only handle if unpressed while on object
            if (oldValue == 1 && newValue == 0 && hoverField.GetValue() == 1)
            {
                onClick?.Invoke();
            }
        }
    }
}
