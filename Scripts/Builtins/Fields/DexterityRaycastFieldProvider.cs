using System.Collections.Generic;

namespace OneHamsa.Dexterity.Builtins
{
    internal class DexterityRaycastFieldProvider : IRaycastReceiver
    {
        private readonly HashSet<IRaycastController> controllers = new();
        private readonly HashSet<IRaycastController> receivedPressStart = new();

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
                foreach (var ctrl in controllers) {
                    if (!string.IsNullOrEmpty(castTag) && !ctrl.CompareTag(castTag))
                        continue;

                    if (ctrl.isPressed && receivedPressStart.Contains(ctrl))
                        return true;
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
            controllers.Remove(controller);
            receivedPressStart.Remove(controller);
        }
    }
}