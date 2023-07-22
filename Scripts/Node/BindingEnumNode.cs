using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    [AddComponentMenu("Dexterity/Binding Enum Node")]
    [DefaultExecutionOrder(Manager.nodeExecutionPriority)]
    public class BindingEnumNode : BaseEnumStateNode
    {
        public EnumOrBoolObjectBinding binding = new();

        [Header("Boolean Source")] 
        public string booleanTrueState = "On";
        public string booleanFalseState = "Off";

        public int targetEnumValue => binding.IsInitialized() ? binding.GetValueAsInt() : 0;
        public Type targetEnumType => binding.IsInitialized() ? binding.type : null;

        public void InitializeBinding()
        {
            if (!binding.IsValid()) 
                return;
            
            if (!binding.Initialize() && Application.isPlaying)
            {
                Debug.LogError($"Failed to initialize binding for {name}", this);
                enabled = false;
            }
        }

        protected override void Initialize()
        {
            InitializeBinding();
            base.Initialize();
        }

        protected override IEnumerable<(string enumOption, int enumValue)> GetEnumOptions()
        {
            if (targetEnumType == null)
            {
                yield return (initialState, 0);
                yield break;
            }

            // bool
            if (targetEnumType == typeof(bool))
            {
                yield return (booleanFalseState, 0);
                yield return (booleanTrueState, 1);
                yield break;
            }
            
            // enum
            foreach (var enumOption in Enum.GetNames(targetEnumType)) 
            {
                yield return (enumOption, (int)Enum.Parse(targetEnumType, enumOption));
            }
        }
        public override int GetEnumValue() => Convert.ToInt32(targetEnumValue);
        
        protected override void UpdateInternal(bool ignoreDelays)
        {
            // since this type of node is using a data source, state should always be considered dirty
            stateDirty = true;
            
            base.UpdateInternal(ignoreDelays);
        }

        protected override void OnValidate() 
        {
            base.OnValidate();
            
            if (Application.isPlaying) 
                return;
            
            try 
            {
                // cache for sake of showing options in editor (enumToStateId.Keys)
                InitializeBinding();
            } catch (ArgumentException) 
            {
                // it's ok in editor!
            }
            CacheEnumOptions();
        }
    }
}
