using System.Collections;
using System.Collections.Generic;
using OneHamsa.Dexterity.Builtins;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    public interface IRaycastResolver
    {
        bool GetHit(Ray ray, out DexterityRaycastHit hit);
        IRaycastReceiver GetReceiver();
        LayerMask GetLayerMask();
    }
}
