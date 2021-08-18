using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class AndField : BaseField
    {
        [SerializeReference]
        public BaseField first;
        [SerializeReference]
        public BaseField second;

        // TODO
        public override int GetValue() {
            return first.GetValue() == 1 && second.GetValue() == 1 ? 1 : 0;
        }

        public override void Initialize(Node context) {
            base.Initialize(context);

            ClearUpstreamFields();

            AddUpstreamField(first);
            AddUpstreamField(second);
        }
    }
}
