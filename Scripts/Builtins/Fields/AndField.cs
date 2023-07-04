using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace OneHamsa.Dexterity.Builtins
{
    public class AndField : BaseField
    {
        [SerializeReference]
        public BaseField first;
        [SerializeReference]
        public BaseField second;

        public override int GetValue() {
            return first?.GetValue() == 1 && second?.GetValue() == 1 ? 1 : 0;
        }

        protected override void Initialize(FieldNode context) {
            base.Initialize(context);

            ClearUpstreamFields();

            AddUpstreamField(first);
            AddUpstreamField(second);
        }
        
        public override void RebuildCache() {
            first.RebuildCache();
            second.RebuildCache();
        }
    }
}
