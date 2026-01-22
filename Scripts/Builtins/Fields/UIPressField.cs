using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
    [Preserve]
    [System.Serializable]
    public class UIPressField : BaseField
    {
        DexterityUIPressFieldProvider provider = null;
        public class DexterityUIPressFieldProvider : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
        {
            internal UIPressField field;
            
            public void OnPointerDown(PointerEventData eventData) => field.SetValue(1);
            public void OnPointerUp(PointerEventData eventData) => field.SetValue(0);
        }

        protected override void Initialize(FieldNode context)
        {
            base.Initialize(context);

            provider = context.gameObject.AddComponent<DexterityUIPressFieldProvider>();
            provider.field = this;
        }
        public override void Uninitialize(FieldNode context)
        {
            base.Uninitialize(context);

            UnityEngine.Object.Destroy(provider);
        }
    }
}
