using System;
using UnityEngine;

namespace OneHamsa.Dexterity.Builtins
{
    /// <summary>
    /// HierarchyNode equivalent of <see cref="EnumField"/>.
    /// Reports its state while the referenced <see cref="BindingEnumNode"/> is in
    /// the named enum case.
    /// </summary>
    [AddComponentMenu("Dexterity/Hierarchy/Providers/Enum Provider")]
    public class EnumProvider : HierarchyStateProvider
    {
        public BindingEnumNode targetNode;

        [EnumField(nameof(targetNode))]
        public string targetField;

        [Tooltip("Invert the equality result.")]
        public bool negate;

        private int _cachedEnumValue;
        private bool _initialized;
        private bool _lastActive;

        protected override void OnEnable()
        {
            base.OnEnable();
            _initialized = false;

            if (targetNode == null)
            {
                Debug.LogError($"{nameof(EnumProvider)} on {name}: targetNode is null", this);
                return;
            }

            targetNode.InitializeBinding();
            if (targetNode.bindingType == null || string.IsNullOrEmpty(targetField))
                return;

            try
            {
                _cachedEnumValue = Convert.ToInt32(Enum.Parse(targetNode.bindingType, targetField));
                _initialized = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"{nameof(EnumProvider)} on {name}: failed to parse '{targetField}': {e.Message}", this);
            }
        }

        protected override bool ComputeIsActive()
        {
            if (!_initialized || targetNode == null || !targetNode.initialized)
                return negate;

            var equal = targetNode.GetEnumValue() == _cachedEnumValue;
            return equal ^ negate;
        }

        private void Update()
        {
            var now = ComputeIsActive();
            if (now != _lastActive)
            {
                _lastActive = now;
                MarkChanged();
            }
        }
    }
}
