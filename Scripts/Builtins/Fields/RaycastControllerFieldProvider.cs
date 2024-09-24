using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace OneHamsa.Dexterity.Builtins
{
    public class RaycastControllerFieldProvider : IRaycastReceiver
    {
        private List<IRaycastController> controllers = new();
        private List<IRaycastController> receivedPressStart = new();
        private List<IRaycastController> controllersToClear = new();
        
        public bool stayPressedOutOfBounds;

        public event Action onChanged;

        public bool GetHover(string castTag = null)
        {
            // foreach enumerator not cached for interfaces on IL2CPP
            for (var index = 0; index < controllers.Count; index++)
            {
                var ctrl = controllers[index];
                if (!string.IsNullOrEmpty(castTag) && !ctrl.CompareTag(castTag))
                    continue;
                return true;
            }

            return false;
        }
        public bool GetPress(string castTag = null)
        {
            controllersToClear.Clear();
            
            // foreach enumerator not cached for interfaces on IL2CPP
            for (var index = 0; index < controllers.Count; index++)
            {
                var ctrl = controllers[index];
                if (!string.IsNullOrEmpty(castTag) && !ctrl.CompareTag(castTag))
                    continue;

                if (ctrl.isPressed && receivedPressStart.Contains(ctrl))
                    return true;

                if (!ctrl.isPressed && stayPressedOutOfBounds)
                {
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

        void IRaycastReceiver.ReceiveHit(IRaycastController controller, ref IRaycastController.RaycastResult hitResult)
        {
            var changed = false;
            if (!controllers.Contains(controller))
            {
                controllers.Add(controller);
                changed = true;
            }

            if (controller.wasPressedThisFrame && !receivedPressStart.Contains(controller))
            {
                receivedPressStart.Add(controller);
                changed = true;
            }
            
            if (changed)
                onChanged?.Invoke();
        }

        void IRaycastReceiver.ClearHit(IRaycastController controller)
        {
            if(stayPressedOutOfBounds && controller.isPressed) return;

            var changed = controllers.Remove(controller);
            changed |= receivedPressStart.Remove(controller);
            
            if (changed)
                onChanged?.Invoke();
        }

        public void ClearAll()
        {
            controllers.Clear();
            receivedPressStart.Clear();
            controllersToClear.Clear();
        }
    }
}