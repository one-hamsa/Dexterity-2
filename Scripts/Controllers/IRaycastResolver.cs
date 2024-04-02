using System.Collections;
using System.Collections.Generic;
using OneHamsa.Dexterity.Builtins;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    public interface IRaycastResolver
    {
        public bool GetHit(Ray ray, out DexRaycastHit hit);
        IRaycastReceiver GetReceiver();
    }
}
