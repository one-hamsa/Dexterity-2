using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public interface IRaycastController
    {
        bool CompareTag(string tag);
        bool isPressed { get; }
        bool wasPressedThisFrame { get; }
        Vector3 position { get; }
        Vector3 forward { get; }

        bool Lock(IRaycastReceiver receiver);
        bool Unlock(IRaycastReceiver receiver);
        bool isLocked { get; }
    }
}
