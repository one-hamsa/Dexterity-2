using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting;

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
        protected FieldNode node;

        [SerializeField]
        public UnityEvent onClick;

        public Settings settings = new Settings {};

        public event Action onPressDown;
        public event Action onPressUp;

        FieldNode.OutputField pressedField;
        FieldNode.OutputField hoverField;
        FieldNode.OutputField disabledField;
        FieldNode.OutputField visibleField;
        private int pressFrame = -1;
        
        [Preserve]
        public bool WasPressedThisFrame() => pressFrame == Time.frameCount - 1;
        
        public FieldNode GetNode() => node;

        protected virtual void Awake()
        {
            if (!node)
                node = GetComponentInParent<FieldNode>();

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

        private void HandlePress(FieldNode.OutputField field, bool oldValue, bool newValue)
        {
            // don't handle if hidden
            if (visibleField != null && !visibleField.GetBooleanValue())
                return;

            // don't handle if disabled
            if (disabledField != null && disabledField.GetBooleanValue())
                return;

            switch (newValue)
            {
                case true when !oldValue:
                    onPressDown?.Invoke();
                    break;
                case false when oldValue:
                    onPressUp?.Invoke();
                    break;
            }

            // manually refresh hover value - we might be in the middle of a cache update
            hoverField.CacheValue();

            // only handle if unpressed while on object
            if (oldValue && !newValue && hoverField.GetBooleanValue())
                OnPressComplete();
        }

        protected virtual void OnPressComplete() => TriggerClick();

        public void TriggerClick()
        {
            pressFrame = Time.frameCount;
            onClick?.Invoke();
        }
    }
}
