using System.Collections.Generic;
using UnityEngine.Pool;

namespace OneHamsa.Dexterity.Builtins
{
    internal struct RaycastControllerFieldProvider : IRaycastReceiver
    {
        private HashSet<IRaycastController> controllers;
        private HashSet<IRaycastController> receivedPressStart;
        private List<IRaycastController> controllersToClear;
        
        public bool stayPressedOutOfBounds;

        public static RaycastControllerFieldProvider Create()
        {
            return new RaycastControllerFieldProvider
            {
                controllers = new HashSet<IRaycastController>(),
                receivedPressStart = new HashSet<IRaycastController>(),
                controllersToClear = new List<IRaycastController>(),
            };
        }

        public bool GetHover(string castTag = null) 
        {
            foreach (var ctrl in controllers) {
                if (!string.IsNullOrEmpty(castTag) && !ctrl.CompareTag(castTag))
                    continue;
                return true;
            }
            return false;
        }
        public bool GetPress(string castTag = null)
        {
            controllersToClear.Clear();
            foreach (var ctrl in controllers) {
                if (!string.IsNullOrEmpty(castTag) && !ctrl.CompareTag(castTag))
                    continue;

                if (ctrl.isPressed && receivedPressStart.Contains(ctrl))
                    return true;

                if (!ctrl.isPressed && stayPressedOutOfBounds) {
                    controllersToClear.Add(ctrl);
                }
            }
            for (int i = 0; i < controllersToClear.Count; i++)
            {
                var controller = controllersToClear[i];
                receivedPressStart.Remove(controller);
                controllers.Remove(controller);
            }
            return false;
        }

        void IRaycastReceiver.ReceiveHit(IRaycastController controller, ref IRaycastController.RaycastEvent raycastEvent)
        {
            controllers.Add(controller);
            if (controller.wasPressedThisFrame)
                receivedPressStart.Add(controller);
        }

        void IRaycastReceiver.ClearHit(IRaycastController controller)
        {
            if(stayPressedOutOfBounds) return;
            controllers.Remove(controller);
            receivedPressStart.Remove(controller);
        }
    }
}