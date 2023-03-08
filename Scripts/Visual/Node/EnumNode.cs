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

        private ObjectEnumContext objectCtx;
        private HashSet<string> enumNames = new();
        private Dictionary<int, string> enumIntOptions = new();
        private Dictionary<string, int> enumToStateId = new();

        public int targetEnumValue => objectCtx?.GetValue() ?? 0;
        public Type targetEnumType => objectCtx?.type;

        public void InitializeObjectContext() {
            if (targetObject != null && !string.IsNullOrEmpty(targetProperty))
                objectCtx = new ObjectEnumContext(this, nameof(targetProperty));
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
            enumNames.Clear();
            enumIntOptions.Clear();
            enumToStateId.Clear();
            if (targetEnumType == null)
                return;

            foreach (var enumOption in Enum.GetNames(targetEnumType)) {
                enumNames.Add(enumOption);
                enumIntOptions.Add((int)Enum.Parse(targetEnumType, enumOption), enumOption);
                enumToStateId.Add(enumOption, Core.instance?.GetStateID(enumOption) ?? -1);
            }
        }

        public override HashSet<string> GetFieldNames() => enumNames;
        public override HashSet<string> GetStateNames() => enumNames;
        
        public int GetEnumValue() => Convert.ToInt32(targetEnumValue);
        public string GetEnumValueAsString()
        {
            enumIntOptions.TryGetValue(GetEnumValue(), out var value);
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
