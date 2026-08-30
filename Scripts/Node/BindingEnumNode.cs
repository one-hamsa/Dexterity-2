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

        /// <summary>
        /// an unresolved binding reports the boolean fallback state list (see <see cref="GetEnumOptions"/>).
        /// syncing modifiers against that overwrites their real states, so this requires a binding that
        /// actually resolved - being configured is not enough.
        /// </summary>
        public override bool ShouldAutoSyncModifiersStates()
        {
            return base.ShouldAutoSyncModifiersStates() && binding.IsInitialized();
        }

        public Type bindingType => binding.IsInitialized() ? binding.type : null;

        public void InitializeBinding()
        {
            // IsResolved() means the lookup already ran against these exact values. a binding that is
            // unresolved only because its target had not loaded yet never stamps, so it retries on the
            // next call, while a binding naming a member that does not exist stamps and reports once.
            if (binding.IsResolved())
                return;

            // an unconfigured binding is not a failure - it has a boolean fallback state list.
            if (binding.Initialize() || !binding.IsValid())
                return;

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
                // an absent binding legitimately falls back to a boolean state list. a binding that has
                // not resolved yet must not: OnValidate runs while the scene is still loading, and
                // reporting the fallback there lets the editor write it into initialState and into every
                // modifier's state list. an empty list is skipped by both instead.
                if (binding.IsValid() && !Application.IsPlaying(this))
                    yield break;

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
        
        protected override void RefreshInternal(bool ignoreDelays)
        {
            // since this type of node is using a data source, state should always be considered dirty
            stateDirty = true;
            
            base.RefreshInternal(ignoreDelays);
        }

        protected override void OnValidate() 
        {
#if UNITY_EDITOR
            // resolve before base reads GetStateNames(): it rewrites initialState to the first state it
            // knows about, so it must not see a list built from an unresolved binding.
            RefreshStates_Editor();
#endif

            base.OnValidate();
        }

#if UNITY_EDITOR
        /// <summary>
        /// <see cref="OnValidate"/> runs while the scene is loading, where the binding target may not be
        /// reachable yet, and nothing re-validates the node afterwards - so the state list stays cold
        /// until something asks for it again.
        /// </summary>
        public override void RefreshStates_Editor()
        {
            if (Application.IsPlaying(this) || binding.IsResolved())
                return;

            try
            {
                // binding.type feeds GetEnumOptions() below and isn't serialized, so it has to be
                // resolved here.
                InitializeBinding();
            }
            catch (ArgumentException)
            {
                // it's ok in editor!
            }
            CacheEnumOptions();
        }
#endif

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
