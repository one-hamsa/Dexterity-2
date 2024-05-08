using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace OneHamsa.Dexterity.Builtins
{
    public class RaycastControllerFieldProvider : IRaycastReceiver
    {
        private HashSet<IRaycastController> controllers = new();
        private HashSet<IRaycastController> receivedPressStart = new();
        private List<IRaycastController> controllersToClear = new();
        
        public bool stayPressedOutOfBounds;

        public event Action onChanged;

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
            var changed = controllers.Add(controller);
            if (controller.wasPressedThisFrame)
                changed |= receivedPressStart.Add(controller);
            
            if (changed)
                onChanged?.Invoke();
        }

        void IRaycastReceiver.ClearHit(IRaycastController controller)
        {
            if(stayPressedOutOfBounds) return;

            var changed = controllers.Remove(controller);
            changed |= receivedPressStart.Remove(controller);
            
            if (changed)
                onChanged?.Invoke();
        }
    }
}