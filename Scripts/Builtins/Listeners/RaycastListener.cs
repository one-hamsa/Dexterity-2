using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace OneHamsa.Dexterity.Builtins
{
    public class RaycastListener : MonoBehaviour, IRaycastReceiver
    {
        public event Action onPress;
        public event Action onRelease;
        public bool pressing => pressingController != null;

        [NonSerialized]
        public readonly List<IRaycastController> hoveringControllers = new();
        public IRaycastController pressingController { get; private set; }
        private RaycastHit lastHit;

        void IRaycastReceiver.ReceiveHit(IRaycastController controller, ref IRaycastController.RaycastResult hitResult)
        {
            if (!hoveringControllers.Contains(controller))
                hoveringControllers.Add(controller);
        }

        void IRaycastReceiver.ClearHit(IRaycastController controller)
        {
            hoveringControllers.Remove(controller);
        }

        public void SetPressing(IRaycastController controller) {
            if (controller == null) {
                if (pressingController == null) return;
                pressingController = null;
                onRelease?.Invoke();
            } else {
                pressingController = controller;
                onPress?.Invoke();
            }
        }

        private void LateUpdate() {
            if (!pressing)
            {
                foreach (var controller in hoveringControllers)
                {
                    if (controller.wasPressedThisFrame)
                    {
                        SetPressing(controller);
                        break;
                    }
                }
            }
            else if (!pressingController.isPressed) {
                SetPressing(null);
            }
        }
    }
}
