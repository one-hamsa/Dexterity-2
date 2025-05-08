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
        private bool initializedFromInitialState;

        protected override IEnumerable<(string enumOption, int enumValue)> GetEnumOptions()
        {
            for (int i = 0; i < manualStates.Count; i++)
            {
                yield return (manualStates[i], i);
            }
        }

        protected override void Initialize()
        {
            // once per lifetime, set the state from the initial state
            if (!initializedFromInitialState)
            {
                activeStateIndex = manualStates.IndexOf(initialState);
                initializedFromInitialState = true;
            }

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
        
        /// <summary>
        /// Get state by name
        /// </summary>
        public string GetStateAsString()
        {
            if (activeStateIndex < 0 || activeStateIndex >= manualStates.Count)
            {
                Debug.LogError($"State index {activeStateIndex} out of bounds in {name}", this);
                return null;
            }
            return manualStates[activeStateIndex];
        }

        #if UNITY_EDITOR
        public void Cache_Editor() => CacheEnumOptions();

        public override void RenameState(string oldStateName, string newStateName)
        {
            base.RenameState(oldStateName, newStateName);
            
            var index = manualStates.IndexOf(oldStateName);
            if (index != -1)
            {
                manualStates[index] = newStateName;
                Debug.Log($"Renamed state {oldStateName} to {newStateName} in {name}", this);
            }
            else
            {
                Debug.LogError($"State {oldStateName} not found in {name}", this);
            }
            
            Cache_Editor();
        }

#endif
    }
}
