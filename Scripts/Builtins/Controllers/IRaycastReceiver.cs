using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public interface IRaycastReceiver
    {
        void ReceiveHit(IRaycastController controller, ref IRaycastController.RaycastEvent hitEvent);
        void ClearHit(IRaycastController controller);
        IRaycastReceiver Resolve() => this;
    }
}
