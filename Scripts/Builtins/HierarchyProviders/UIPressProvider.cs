using UnityEngine;
using UnityEngine.EventSystems;

namespace OneHamsa.Dexterity.Builtins
{
    /// <summary>
    /// HierarchyNode equivalent of <see cref="UIPressField"/>.
    /// Reports its state while a Unity EventSystem pointer is pressed on this UI element.
    /// </summary>
    [AddComponentMenu("Dexterity/Hierarchy/Providers/UI Press Provider")]
    public class UIPressProvider : HierarchyStateProvider, IPointerDownHandler, IPointerUpHandler
    {
        private bool _pressed;

        protected override bool ComputeIsActive() => _pressed;

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
            MarkChanged();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
            MarkChanged();
        }

        protected override void OnDisable()
        {
            _pressed = false;
            base.OnDisable();
        }
    }
}
