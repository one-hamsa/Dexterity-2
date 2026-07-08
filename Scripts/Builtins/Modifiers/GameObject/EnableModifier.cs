using UnityEngine;
using System;

namespace OneHamsa.Dexterity.Builtins
{
    public class EnableModifier : Modifier
    {
        [Tooltip("Component whose 'enabled' flag is driven by the node's state.")]
        [SerializeField] private MonoBehaviour target;

        public override bool animatableInEditor => enabled;

        [Serializable]
        public class Property : PropertyBase
        {
            [Tooltip("Whether the target component is enabled in this state.")]
            public bool enabled = true;
        }

        public override void HandleStateChange(int oldState, int newState)
        {
            base.HandleStateChange(oldState, newState);
            target.enabled = ((Property)GetProperty(newState)).enabled;
        }

        public override void Refresh()
        {
            base.Refresh();
            // bool toggle is discrete — never report a transition in progress
            transitionChanged = false;
        }
    }
}
