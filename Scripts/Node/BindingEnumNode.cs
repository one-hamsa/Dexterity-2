using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    [AddComponentMenu("Dexterity/Binding Enum Node")]
    [DefaultExecutionOrder(Manager.nodeExecutionPriority)]
    public class BindingEnumNode : BaseEnumStateNode
    {
        public GenericObjectBinding binding = new();

        [Header("Boolean Source")] 
        public string booleanTrueState = "On";
        public string booleanFalseState = "Off";
        
        [Header("Int Source")] 
        public int intMinState = 0;
        public int intMaxState = 1;
        public int intOutOfBoundsState = 0;

        [NonSerialized]
        private bool _performedFirstInitialization_BindingEnumNode;

        private StringBuilder sb;

        public int bindingValue
        {
            get
            {
                if (!binding.IsInitialized())
                    return 0;
                
                var value = binding.GetValueAsInt();
                if (bindingType == typeof(int))
                {
                    if (value < intMinState || value > intMaxState)
                        return intOutOfBoundsState;
                }
                
                return value;
            }
        }

        public override bool ShouldAutoSyncModifiersStates()
        {
            return base.ShouldAutoSyncModifiersStates() && binding.IsValid();
        }

        public Type bindingType => binding.IsInitialized() ? binding.type : null;

        public void InitializeBinding()
        {
            if (!binding.IsValid()) 
                return;
            
            if (!binding.Initialize())
            {
                if (Application.IsPlaying(this))
                {
                    Debug.LogError($"Failed to initialize binding for {name}: {binding}", this);
                    enabled = false;
                }
                else
                {
                    Debug.LogWarning($"Failed to initialize binding for {name}: {binding}", this);
                }
            }
        }

        protected override void Initialize()
        {
            if (!_performedFirstInitialization_BindingEnumNode)
                InitializeBinding();
            
            _performedFirstInitialization_BindingEnumNode = true;
            
            base.Initialize();
        }

        protected override IEnumerable<(string enumOption, int enumValue)> GetEnumOptions()
        {
            if (bindingType == null)
            {
                yield return (booleanFalseState, 0);
                yield return (booleanTrueState, 1);
                yield break;
            }

            // bool
            if (bindingType == typeof(bool))
            {
                yield return (booleanFalseState, 0);
                yield return (booleanTrueState, 1);
                yield break;
            }
            
            // int
            if (bindingType == typeof(int))
            {
                for (int i = intMinState; i <= intMaxState; i++)
                {
                    yield return (i.ToString(), i);
                }
                if (intMinState > intOutOfBoundsState || intMaxState < intOutOfBoundsState)
                    yield return (intOutOfBoundsState.ToString(), intOutOfBoundsState);
                yield break;
            }
            
            // enum
            foreach (var enumOption in Enum.GetNames(bindingType)) 
            {
                yield return (enumOption, (int)Enum.Parse(bindingType, enumOption));
            }
        }
        public override int GetEnumValue() => Convert.ToInt32(bindingValue);
        
        protected override void UpdateInternal(bool ignoreDelays)
        {
            // since this type of node is using a data source, state should always be considered dirty
            stateDirty = true;
            
            base.UpdateInternal(ignoreDelays);
        }

        protected override void OnValidate() 
        {
            base.OnValidate();
            
            if (Application.IsPlaying(this)) 
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

        public override string ToString()
        {
            sb ??= new System.Text.StringBuilder();
            sb.Clear();
            
            sb.Append(base.ToString());
            sb.Append(" (");
            sb.Append(binding);
            sb.Append(")");
            
            return sb.ToString();
        }
    }
}
