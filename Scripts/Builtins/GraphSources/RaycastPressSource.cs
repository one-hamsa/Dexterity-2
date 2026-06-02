using UnityEngine;

namespace OneHamsa.Dexterity.Builtins
{
    /// <summary>
    /// GraphNode equivalent of <see cref="RaycastPressField"/>.
    /// Reports its state while any registered <see cref="IRaycastController"/> with
    /// the configured tag is pressing this GameObject's collider.
    /// </summary>
    [AddComponentMenu("Dexterity/Graph/Sources/Raycast Press Source")]
    public class RaycastPressSource : GraphSource, IRaycastReceiver
    {
        [TagSelector, Tooltip("Raycast controller tag filter. Empty = any tag.")]
        public string raycastTag = "Untagged";

        [Tooltip("Keep reporting pressed while the controller stays pressed even after it leaves bounds.")]
        public bool stayPressedOutOfBounds;

        private readonly RaycastControllerFieldProvider _provider = new();
        private bool _lastActive;

        protected override bool ComputeIsActive() => _provider.GetPress(raycastTag);

        protected override void OnEnable()
        {
            base.OnEnable();
            _provider.stayPressedOutOfBounds = stayPressedOutOfBounds;
        }

        private void Update()
        {
            var now = _provider.GetPress(raycastTag);
            if (now != _lastActive)
            {
                _lastActive = now;
                MarkChanged();
            }
        }

        protected override void OnDisable()
        {
            _provider.ClearAll();
            if (_lastActive)
            {
                _lastActive = false;
                MarkChanged();
            }
            base.OnDisable();
        }

        void IRaycastReceiver.ReceiveHit(IRaycastController controller, ref IRaycastController.RaycastResult hitResult)
            => ((IRaycastReceiver)_provider).ReceiveHit(controller, ref hitResult);

        void IRaycastReceiver.ClearHit(IRaycastController controller)
            => ((IRaycastReceiver)_provider).ClearHit(controller);
    }
}
