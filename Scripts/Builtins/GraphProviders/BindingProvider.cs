using UnityEngine;

namespace OneHamsa.Dexterity.Builtins
{
    /// <summary>
    /// GraphNode equivalent of <see cref="BindingField"/>.
    /// Reports its state while the referenced boolean property/method
    /// (configured via <see cref="BoolObjectBinding"/>) evaluates to true.
    /// </summary>
    [AddComponentMenu("Dexterity/Graph/Providers/Binding Provider")]
    public class BindingProvider : GraphStateProvider
    {
        public BoolObjectBinding binding = new();

        [Tooltip("Invert the binding's boolean value.")]
        public bool negate;

        private bool _lastActive;
        private bool _stopped;

        protected override void OnEnable()
        {
            base.OnEnable();
            _stopped = false;
            if (binding != null && binding.IsValid() && !binding.IsInitialized())
            {
                if (!binding.Initialize())
                {
                    Debug.LogError($"{nameof(BindingProvider)}: failed to initialize binding on {name}", this);
                    _stopped = true;
                }
            }
            _lastActive = false;
        }

        protected override bool ComputeIsActive()
        {
            if (_stopped) return negate;
            if (binding == null || !binding.IsInitialized()) return negate;
            if (binding.target is MonoBehaviour mb && !mb.isActiveAndEnabled) return negate;

            try
            {
                return binding.Boolean_GetValue() ^ negate;
            }
            catch (MissingReferenceException)
            {
                _stopped = true;
                return negate;
            }
            catch (System.NullReferenceException)
            {
                if (binding != null && binding.target == null)
                {
                    _stopped = true;
                    return negate;
                }
                throw;
            }
        }

        private void Update()
        {
            if (_stopped) return;

            var now = ComputeIsActive();
            if (now != _lastActive)
            {
                _lastActive = now;
                MarkChanged();
            }
        }
    }
}
