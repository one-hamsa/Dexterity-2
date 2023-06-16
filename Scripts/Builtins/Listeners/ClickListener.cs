using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace OneHamsa.Dexterity.Builtins
{
    public class ClickListener : MonoBehaviour
    {
        [Serializable]
        public class Settings {
            [Field]
            public string pressedFieldName = "pressed";
            [Field]
            public string hoverFieldName = "hover";
            [Field(allowNull: true)]
            public string disabledFieldName = "disabled";
            [Field(allowNull: true)]
            public string visibleFieldName = "visible";
        }

        [SerializeField]
        protected Node node;

        [SerializeField]
        public UnityEvent onClick;

        public Settings settings = new Settings {};

        public event Action onPressDown;

        Node.OutputField pressedField;
        Node.OutputField hoverField;
        Node.OutputField disabledField;
        Node.OutputField visibleField;

        protected virtual void Awake()
        {
            if (!node)
                node = GetComponentInParent<Node>();

            if (!node)
            {
                Debug.LogWarning($"Node not found for listener ({gameObject.name})", this);
                enabled = false;
                return;
            }

        }

        protected virtual void OnEnable()
        {
            pressedField = node.GetOutputField(settings.pressedFieldName);
            hoverField = node.GetOutputField(settings.hoverFieldName);
            if (!string.IsNullOrEmpty(settings.disabledFieldName))
                disabledField = node.GetOutputField(settings.disabledFieldName);
            if (!string.IsNullOrEmpty(settings.visibleFieldName))
                visibleField = node.GetOutputField(settings.visibleFieldName);

            pressedField.onBooleanValueChanged += HandlePress;
        }
        protected virtual void OnDisable()
        {
            if (pressedField != null)
                pressedField.onBooleanValueChanged -= HandlePress;
        }

        private void HandlePress(Node.OutputField field, bool oldValue, bool newValue)
        {
            // don't handle if hidden
            if (visibleField != null && !visibleField.GetBooleanValue())
                return;

            // don't handle if disabled
            if (disabledField != null && disabledField.GetBooleanValue())
                return;

            if (newValue && !oldValue)
                onPressDown?.Invoke();

            // manually refresh hover value - we might be in the middle of a cache update
            hoverField.CacheValue();

            // only handle if unpressed while on object
            if (oldValue && !newValue && hoverField.GetBooleanValue())
                Click();
        }

        public void Click() => onClick?.Invoke();
    }
}
