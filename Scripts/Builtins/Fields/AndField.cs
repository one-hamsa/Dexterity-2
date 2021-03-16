using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class AndField : BaseField
    {
        [SerializeReference]
        public BaseField First;
        [SerializeReference]
        public BaseField Second;

        // TODO
        public override int GetValue() {
            return First.GetValue() == 1 && Second.GetValue() == 1 ? 1 : 0;
        }

        public override void Initialize(Node context) {
            base.Initialize(context);

            ClearUpstreamFields();

            AddUpstreamField(First);
            AddUpstreamField(Second);
        }
    }
}
