using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Builtins
{
    /// <summary>
    /// Per-node override table mapping specific <see cref="BaseStateNode"/> states to pointer
    /// hit-results (<see cref="IRaycastController.RaycastResult.Result"/>).
    ///
    /// A node normally maps its active state through the global
    /// <see cref="DexteritySettings.GetResultFromState"/> table (Hover → CanAccept,
    /// Pressed → Accepted, Disabled → CannotAccept). That only knows the canonical states.
    /// Add this component to the node's GameObject to map extra/custom states (e.g. a
    /// "Selected" or "Highlighted" state that should also read as CanAccept). States not
    /// listed here fall back to the global table, so canonical states keep working.
    /// </summary>
    [AddComponentMenu("Dexterity/Hit Result Mapping")]
    public class HitResultMapping : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("State name on this GameObject's node (matches a Dexterity state, e.g. \"Selected\")."), State]
            public string state;

            [Tooltip("Pointer result reported while the node is in this state.")]
            public IRaycastController.RaycastResult.Result result;
        }

        [SerializeField, Tooltip("Extra state → result overrides. States not listed here fall back to the " +
            "global DexteritySettings table (Hover → CanAccept, Pressed → Accepted, ...).")]
        private List<Entry> mappings = new();

        /// <summary>
        /// Resolves the pointer result for <paramref name="node"/>'s current active state: a
        /// <see cref="HitResultMapping"/> override on the same GameObject wins; otherwise the
        /// global <see cref="DexteritySettings"/> table is used. This is the single entry point
        /// both FieldNode and GraphNode use to report their result to the pointer.
        /// </summary>
        public static IRaycastController.RaycastResult.Result Resolve(BaseStateNode node)
        {
            var state = node.GetActiveState();
            if (node.TryGetComponent(out HitResultMapping mapping) && mapping.TryGetResult(state, out var result))
                return result;

            return Manager.instance.settings.GetResultFromState(state);
        }

        public bool TryGetResult(int activeStateId, out IRaycastController.RaycastResult.Result result)
        {
            for (var i = 0; i < mappings.Count; i++)
            {
                var stateName = mappings[i].state;
                if (!string.IsNullOrEmpty(stateName) && Database.instance.GetStateID(stateName) == activeStateId)
                {
                    result = mappings[i].result;
                    return true;
                }
            }

            result = IRaycastController.RaycastResult.Result.Default;
            return false;
        }
    }
}
