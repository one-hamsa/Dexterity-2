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
        private Dictionary<int, string> enumOptions = new();
        private Dictionary<string, int> enumToStateId = new();

        public Enum targetEnumValue => objectCtx?.GetValue<Enum>();
        public Type targetEnumType => objectCtx?.type;

        public void InitializeObjectContext() {
            if (targetObject != null && !string.IsNullOrEmpty(targetProperty))
                objectCtx = ObjectValueAttribute.CreateContext(this, nameof(targetProperty));
        }

        protected override void Initialize()
        {
            InitializeObjectContext();
            //cache that state names to register
            CacheEnumOptions();
            //registers them
            base.Initialize();
            //gets the state IDs from the manager
            CacheEnumOptions();
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
        
        public int GetEnumValue() => Convert.ToInt32(targetEnumValue);
        public string GetEnumValueAsString()
        {
            enumOptions.TryGetValue(GetEnumValue(), out var value);
            return value;
        }

        protected override int GetState()
        {
            var baseState = base.GetState();
            if (baseState != StateFunction.emptyStateId)
                return baseState;

            var enumValue = GetEnumValueAsString();
            if (string.IsNullOrEmpty(enumValue))
            {
                Debug.LogError($"internal error: enumValue == null after initialization", this);
                enabled = false;
                return StateFunction.emptyStateId;
            }

            return enumToStateId[enumValue];
        }

        protected override void UpdateInternal(bool ignoreDelays)
        {
            // since this type of node is using a data source, state should always be considered dirty
            stateDirty = true;
            
            base.UpdateInternal(ignoreDelays);
        }

        protected override void OnValidate() {
            base.OnValidate();
            
            if (Application.isPlaying) 
                return;
            
            try {
                InitializeObjectContext();
            } catch (ArgumentException) {
                // it's ok in editor!
            }
            
            // cache for sake of showing options in editor (enumToStateId.Keys)
            CacheEnumOptions();
        }
    }
}
