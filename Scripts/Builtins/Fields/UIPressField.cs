using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.EventSystems;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class UIPressField : BaseField
    {
        DexterityUIPressFieldProvider provider;
        public class DexterityUIPressFieldProvider : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
        {
            public bool Click { get;  private set; }
            public void OnPointerDown(PointerEventData eventData) => Click = true;
            public void OnPointerUp(PointerEventData eventData) => Click = false;
        }

        public override void Initialize(Node context)
        {
            base.Initialize(context);

            provider = context.gameObject.AddComponent<DexterityUIPressFieldProvider>();
        }

        public override int GetValue() => provider.Click ? 1 : 0;
    }
}
