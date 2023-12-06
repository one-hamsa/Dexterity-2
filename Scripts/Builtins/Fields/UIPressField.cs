using OneHamsa.Dexterity.Utilities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
    [Preserve]
    public class UIPressField : BaseField
    {
        DexterityUIPressFieldProvider provider = null;
        public class DexterityUIPressFieldProvider : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
        {
            public bool click { get;  private set; }
            public void OnPointerDown(PointerEventData eventData) => click = true;
            public void OnPointerUp(PointerEventData eventData) => click = false;
        }

        protected override void Initialize(FieldNode context)
        {
            base.Initialize(context);

            provider = context.gameObject.GetOrAddComponent<DexterityUIPressFieldProvider>();
        }
        public override void Finalize(FieldNode context)
        {
            base.Finalize(context);

            UnityEngine.Object.Destroy(provider);
        }

        public override int GetValue() => (provider && provider.click) ? 1 : 0;
    }
}
