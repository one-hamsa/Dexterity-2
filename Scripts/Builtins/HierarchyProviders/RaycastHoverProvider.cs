using UnityEngine;

namespace OneHamsa.Dexterity.Builtins
{
    /// <summary>
    /// HierarchyNode equivalent of <see cref="RaycastHoverField"/>.
    /// Reports its state while any registered <see cref="IRaycastController"/> with
    /// the configured tag is hovering this GameObject's collider.
    /// </summary>
    [AddComponentMenu("Dexterity/Hierarchy/Providers/Raycast Hover Provider")]
    public class RaycastHoverProvider : HierarchyStateProvider, IRaycastReceiver
    {
        [TagSelector, Tooltip("Raycast controller tag filter. Empty = any tag.")]
        public string raycastTag = "Untagged";

        private readonly RaycastControllerFieldProvider _provider = new();
        private bool _lastActive;

        protected override bool ComputeIsActive() => _provider.GetHover(raycastTag);

        private void Update()
        {
            var now = _provider.GetHover(raycastTag);
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
