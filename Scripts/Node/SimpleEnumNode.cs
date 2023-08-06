using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    [AddComponentMenu("Dexterity/Simple Enum Node")]
    [DefaultExecutionOrder(Manager.nodeExecutionPriority)]
    public class SimpleEnumNode : BaseEnumStateNode
    {
        public List<string> manualStates = new();
        private int activeStateIndex;

        protected override IEnumerable<(string enumOption, int enumValue)> GetEnumOptions()
        {
            for (int i = 0; i < manualStates.Count; i++)
            {
                yield return (manualStates[i], i);
            }
        }

        protected override void Initialize()
        {
            activeStateIndex = manualStates.IndexOf(initialState);
            base.Initialize();
        }

        public override int GetEnumValue() => activeStateIndex;

        /// <summary>
        /// Set state by name
        /// </summary>
        /// <param name="state"></param>
        public void SetState(string state)
        {
            var indexOf = manualStates.IndexOf(state);
            if (indexOf == -1)
            {
                Debug.LogError($"State {state} not found in {name}", this);
                return;
            }
            
            activeStateIndex = indexOf;
            stateDirty = true;
        }

        #if UNITY_EDITOR
        public void Cache_Editor() => CacheEnumOptions();
        #endif
    }
}
