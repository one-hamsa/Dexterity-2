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
            public bool Click { get;  private set; }
            public void OnPointerDown(PointerEventData eventData) => Click = true;
            public void OnPointerUp(PointerEventData eventData) => Click = false;

            private void OnEnable() {
                InputManager.instance.OnUp += Instance_OnUp;
            }

            private void Instance_OnUp() {
                Click = false;
            }

            private void OnDisable() {
                InputManager.instance.OnUp += Instance_OnUp;
            }
        }

        public override void Initialize(Node context)
        {
            base.Initialize(context);

            provider = context.gameObject.AddComponent<DexterityUIPressFieldProvider>();
        }

        public override int GetValue() => (provider && provider.Click) ? 1 : 0;
    }
}
