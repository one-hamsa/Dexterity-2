using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
    [Preserve]
    public class UIHoverField : BaseField
    {
        DexterityUIHoverFieldProvider provider = null;
        public class DexterityUIHoverFieldProvider : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            internal UIHoverField field;

            public void OnPointerEnter(PointerEventData eventData) => field.SetValue(1);
            public void OnPointerExit(PointerEventData eventData) => field.SetValue(0);
        }

        protected override void Initialize(FieldNode context)
        {
            base.Initialize(context);
            provider = context.gameObject.AddComponent<DexterityUIHoverFieldProvider>();
            provider.field = this;
        }
        public override void Uninitialize(FieldNode context)
        {
            base.Uninitialize(context);

            UnityEngine.Object.Destroy(provider);
        }
    }
}
