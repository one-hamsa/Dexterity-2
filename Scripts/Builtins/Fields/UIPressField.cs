using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.EventSystems;

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

        public override void Initialize(Node context)
        {
            base.Initialize(context);

            provider = context.gameObject.AddComponent<DexterityUIPressFieldProvider>();
        }

        public override int GetValue() => (provider && provider.click) ? 1 : 0;
    }
}
