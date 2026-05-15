// FieldNode-driven click listener — see BaseClickListener.cs for shared semantics.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace OneHamsa.Dexterity.Builtins
{
    /// <summary>
    /// Click listener for a <see cref="FieldNode"/>. Reads its press/hover/
    /// disabled/visible signals from named <see cref="FieldNode.OutputField"/>s.
    ///
    /// Press notifications are buffered into LateUpdate to avoid reading
    /// upstream field caches mid-pipeline (the Manager update can fire field
    /// onValueChanged while a downstream field's cached value is still stale).
    /// </summary>
    public class FieldNodeClickListener : BaseClickListener
    {
        [Serializable]
        public class Settings
        {
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

        public Settings settings = new();

        private FieldNode.OutputField pressedField;
        private FieldNode.OutputField hoverField;
        private FieldNode.OutputField disabledField;
        private FieldNode.OutputField visibleField;

        private readonly List<BaseField.ValueChangeEvent> pendingEvents = new();

        public FieldNode GetNode() => node;

        protected virtual void Awake()
        {
            if (!node) node = GetComponentInParent<FieldNode>();
            if (!node)
            {
                Debug.LogWarning($"FieldNode not found for listener ({gameObject.name})", this);
                enabled = false;
            }
        }

        protected virtual void OnEnable()
        {
            pressedField = node.GetOutputField(settings.pressedFieldName);
            hoverField   = node.GetOutputField(settings.hoverFieldName);
            if (!string.IsNullOrEmpty(settings.disabledFieldName))
                disabledField = node.GetOutputField(settings.disabledFieldName);
            if (!string.IsNullOrEmpty(settings.visibleFieldName))
                visibleField = node.GetOutputField(settings.visibleFieldName);

            pressedField.onValueChanged += HandlePress;
        }

        protected virtual void OnDisable()
        {
            if (pressedField != null)
                pressedField.onValueChanged -= HandlePress;
            pendingEvents.Clear();
        }

        private void HandlePress(BaseField.ValueChangeEvent e)
        {
            pendingEvents.Add(e);
        }

        private void LateUpdate()
        {
            if (pendingEvents.Count == 0) return;

            using var _ = ListPool<BaseField.ValueChangeEvent>.Get(out var eventsToProcess);
            eventsToProcess.AddRange(pendingEvents);
            pendingEvents.Clear();

            foreach (var _e in eventsToProcess)
            {
                // RefreshUpstreams ensures hover is up-to-date — field-cache pipeline
                // may have left it stale at the moment the press event fired.
                if (hoverField != null) hoverField.RefreshUpstreams();
                OnPressMayHaveChanged();
            }
        }

        protected override bool IsPressed() => pressedField != null && pressedField.GetBooleanValue();
        protected override bool IsHover() => hoverField == null || hoverField.GetBooleanValue();
        protected override bool IsDisabled() => disabledField != null && disabledField.GetBooleanValue();
        protected override bool IsHidden() => visibleField != null && !visibleField.GetBooleanValue();
    }
}
