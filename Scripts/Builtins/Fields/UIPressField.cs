using UnityEngine;
using UnityEngine.EventSystems;
using OneHumus;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class UIPressField : BaseField
    {
        DexterityUIPressFieldProvider provider = null;
        public class DexterityUIPressFieldProvider : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
        {
            public bool click { get;  private set; }
            public void OnPointerDown(PointerEventData eventData) => click = true;
            public void OnPointerUp(PointerEventData eventData) => click = false;
        }

        protected override void Initialize(Node context)
        {
            base.Initialize(context);

            provider = context.gameObject.GetOrAddComponent<DexterityUIPressFieldProvider>();
        }
        public override void Finalize(Node context)
        {
            base.Finalize(context);

            UnityEngine.Object.Destroy(provider);
        }

        public override int GetValue() => (provider && provider.click) ? 1 : 0;
    }
}
