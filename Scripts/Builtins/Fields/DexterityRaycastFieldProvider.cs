using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    internal class DexterityRaycastFieldProvider : MonoBehaviour, IRaycastReceiver
    {
        HashSet<IRaycastController> controllers = new HashSet<IRaycastController>();
        public bool hover => controllers.Count > 0;
        public bool press
        {
            get
            {
                foreach (var ctrl in controllers)
                    if (ctrl.isPressed)
                        return true;
                return false;
            }
        }

        public void ReceiveHit(IRaycastController controller, RaycastHit hit)
        {
            controllers.Add(controller);
        }

        public void ClearHit(IRaycastController controller)
        {
            controllers.Remove(controller);
        }
    }
}