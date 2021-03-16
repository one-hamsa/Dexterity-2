using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.EventSystems;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class UIHoverField : BaseField
    {
        DexterityUIHoverFieldProvider provider;
        public class DexterityUIHoverFieldProvider : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public bool Hover { get;  private set; }
            public void OnPointerEnter(PointerEventData eventData) => Hover = true;
            public void OnPointerExit(PointerEventData eventData) => Hover = false;
        }

        public override void Initialize(Node context)
        {
            base.Initialize(context);
            provider = context.gameObject.AddComponent<DexterityUIHoverFieldProvider>();
        }

        public override int GetValue() => provider.Hover ? 1 : 0;
    }
}
