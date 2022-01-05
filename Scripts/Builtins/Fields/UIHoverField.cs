using UnityEngine;
using UnityEngine.EventSystems;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class UIHoverField : BaseField
    {
        DexterityUIHoverFieldProvider provider = null;
        public class DexterityUIHoverFieldProvider : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public bool hover { get;  private set; }
            public void OnPointerEnter(PointerEventData eventData) => hover = true;
            public void OnPointerExit(PointerEventData eventData) => hover = false;
        }

        protected override void Initialize(Node context)
        {
            base.Initialize(context);
            provider = context.gameObject.GetOrAddComponent<DexterityUIHoverFieldProvider>();
        }
        public override void Finalize(Node context)
        {
            base.Finalize(context);

            UnityEngine.Object.Destroy(provider);
        }

        public override int GetValue() => (provider && provider.hover) ? 1 : 0;
    }
}
