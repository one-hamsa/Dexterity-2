using UnityEngine;
using UnityEngine.EventSystems;

namespace OneHamsa.Dexterity.Builtins
{
    /// <summary>
    /// GraphNode equivalent of <see cref="UIPressField"/>.
    /// Reports its state while a Unity EventSystem pointer is pressed on this UI element.
    /// </summary>
    [AddComponentMenu("Dexterity/Graph/Sources/UI Press Source")]
    public class UIPressSource : GraphSource, IPointerDownHandler, IPointerUpHandler
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
