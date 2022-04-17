using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    [AddComponentMenu("Dexterity/Dexterity Enum Node")]
    [DefaultExecutionOrder(Manager.nodeExecutionPriority)]
    public class EnumNode : DexterityBaseNode
    {
        public UnityEngine.Object targetObject;
        [ObjectValue(objectFieldName: nameof(targetObject), fieldType: typeof(Enum))]
        public string targetProperty;

        private ObjectValueAttribute.Context objectCtx;
        private Dictionary<int, string> enumOptions = new Dictionary<int, string>();
        private Dictionary<string, int> enumToStateId = new Dictionary<string, int>();

        private Enum targetEnumValue => objectCtx?.GetValue<Enum>();
        private Type targetEnumType => objectCtx?.type;

        private void InitializeObjectContext() {
            if (targetObject != null && !string.IsNullOrEmpty(targetProperty))
                objectCtx = ObjectValueAttribute.CreateContext(this, nameof(targetProperty));
        }

        protected override void Initialize()
        {
            InitializeObjectContext();
            CacheEnumOptions();
            base.Initialize();
        }

        private void CacheEnumOptions()
        {
            enumOptions.Clear();
            enumToStateId.Clear();
            if (targetEnumType == null)
                return;

            foreach (var enumOption in Enum.GetNames(targetEnumType)) {
                enumOptions.Add((int)Enum.Parse(targetEnumType, enumOption), enumOption);
                enumToStateId.Add(enumOption, Core.instance?.GetStateID(enumOption) ?? -1);
            }
        }

        public override IEnumerable<string> GetFieldNames() => enumToStateId.Keys;
        public override IEnumerable<string> GetStateNames() => enumToStateId.Keys;

        protected override int GetState()
        {
            var baseState = base.GetState();
            if (baseState != StateFunction.emptyStateId)
                return baseState;

            var value = targetEnumValue;
            return enumToStateId[enumOptions[Convert.ToInt32(value)]];
        }

        private void OnValidate() {
            try {
                InitializeObjectContext();
            } catch (ArgumentException) {
                // it's ok in editor!
            }
            CacheEnumOptions();
        }
    }
}
