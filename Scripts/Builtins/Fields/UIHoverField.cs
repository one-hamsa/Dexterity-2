using OneHamsa.Dexterity.Utilities;
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
            public bool hover { get;  private set; }
            public void OnPointerEnter(PointerEventData eventData) => hover = true;
            public void OnPointerExit(PointerEventData eventData) => hover = false;
        }

        protected override void Initialize(FieldNode context)
        {
            base.Initialize(context);
            provider = context.gameObject.GetOrAddComponent<DexterityUIHoverFieldProvider>();
        }
        public override void Finalize(FieldNode context)
        {
            base.Finalize(context);

            UnityEngine.Object.Destroy(provider);
        }

        public override bool GetValue() => provider && provider.hover;
    }
}
