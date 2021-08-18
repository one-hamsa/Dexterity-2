using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.EventSystems;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class RaycastPressField : BaseField
    {
        DexterityRaycastFieldProvider provider = null;

        public override void Initialize(Node context)
        {
            base.Initialize(context);

            provider = context.gameObject.AddComponent<DexterityRaycastFieldProvider>();
        }

        public override int GetValue() => (provider && provider.press) ? 1 : 0;
    }
}
