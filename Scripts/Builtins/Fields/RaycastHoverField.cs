using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.EventSystems;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class RaycastHoverField : BaseField
    {
        DexterityRaycastFieldProvider provider = null;

        public override void Initialize(Node context)
        {
            base.Initialize(context);

            provider = context.gameObject.AddComponent<DexterityRaycastFieldProvider>();
        }

        public override int GetValue() => (provider && provider.hover) ? 1 : 0;
    }
}
