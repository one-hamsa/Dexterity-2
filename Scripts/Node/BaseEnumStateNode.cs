using System;
using System.Linq;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    public abstract class BaseEnumStateNode : BaseStateNode
    {
        private readonly HashSet<string> enumNames = new();
        private readonly Dictionary<int, string> enumIntOptions = new();
        private readonly Dictionary<string, int> enumToStateId = new();

        protected override void Initialize()
        {
            // cache that state names to register
            CacheEnumOptions();
            // register them
            base.Initialize();
            // get the state IDs from manager
            CacheEnumToStateID();
        }
        
        protected abstract IEnumerable<(string enumOption, int enumValue)> GetEnumOptions();

        protected void CacheEnumOptions()
        {
            enumNames.Clear();
            enumIntOptions.Clear();

            foreach (var (option, value) in GetEnumOptions()) {
                enumNames.Add(option);
                enumIntOptions.Add(value, option);
            }
        }

        private void CacheEnumToStateID()
        {
            enumToStateId.Clear();
            foreach (var enumOption in enumNames) {
                enumToStateId.Add(enumOption, Database.instance?.GetStateID(enumOption) ?? -1);
            }
        }

        public override HashSet<string> GetFieldNames() => enumNames;
        public override HashSet<string> GetStateNames() => enumNames;
        
        /// <summary>
        /// returns current enum value as an int
        /// </summary>
        /// <returns></returns>
        public abstract int GetEnumValue();
        
        /// <summary>
        /// returns current enum value as a string
        /// </summary>
        /// <returns></returns>
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
                Debug.LogError($"Could not find enum value (int value is {GetEnumValue()}, did the enum change?)", this);
                enabled = false;
                return StateFunction.emptyStateId;
            }

            return enumToStateId[enumValue];
        }

        protected override void OnValidate() {
            base.OnValidate();
            
            if (Application.isPlaying) 
                return;
            
            // cache for sake of showing options in editor
            CacheEnumOptions();
        }
        
        #if UNITY_EDITOR
        public override void InitializeEditor() => Initialize();
        #endif
    }
}
