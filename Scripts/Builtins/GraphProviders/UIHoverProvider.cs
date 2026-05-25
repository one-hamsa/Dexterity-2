using UnityEngine;
using UnityEngine.EventSystems;

namespace OneHamsa.Dexterity.Builtins
{
    /// <summary>
    /// GraphNode equivalent of <see cref="UIHoverField"/>.
    /// Reports its state while a Unity EventSystem pointer is hovering this UI element.
    /// </summary>
    [AddComponentMenu("Dexterity/Graph/Providers/UI Hover Provider")]
    public class UIHoverProvider : GraphStateProvider, IPointerEnterHandler, IPointerExitHandler
    {
        private bool _hovered;

        protected override bool ComputeIsActive() => _hovered;

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            MarkChanged();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            MarkChanged();
        }

        protected override void OnDisable()
        {
            _hovered = false;
            base.OnDisable();
        }
    }
}
