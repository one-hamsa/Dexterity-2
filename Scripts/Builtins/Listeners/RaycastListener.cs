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

        public readonly HashSet<IRaycastController> controllers = new();
        public IRaycastController pressingController { get; private set; }
        private RaycastHit lastHit;

        void IRaycastReceiver.ReceiveHit(IRaycastController controller, ref IRaycastController.RaycastEvent hitEvent)
        {
            controllers.Add(controller);
        }

        void IRaycastReceiver.ClearHit(IRaycastController controller)
        {
            controllers.Remove(controller);
        }

        private void LateUpdate() {
            if (!pressing)
            {
                foreach (var controller in controllers)
                {
                    if (controller.wasPressedThisFrame)
                    {
                        pressingController = controllers.First();
                        onPress?.Invoke();
                        break;
                    }
                }
            }
            else if (!pressingController.isPressed) {
                pressingController = null;
                onRelease?.Invoke();
            }
        }
    }
}
