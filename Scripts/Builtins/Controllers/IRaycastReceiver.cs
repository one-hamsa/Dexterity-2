using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public interface IRaycastReceiver
    {
        void ReceiveHit(IRaycastController controller, RaycastHit hit);
        void ClearHit(IRaycastController controller);
        IRaycastReceiver Resolve() => this;
    }
}
