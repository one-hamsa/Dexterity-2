using UnityEngine;

namespace OneHamsa.Dexterity.Builtins
{
    /// <summary>
    /// GraphNode equivalent of <see cref="ConstantField"/>.
    /// Reports its state unconditionally while <see cref="active"/> is true.
    /// Useful as a terminal fallback at the end of a sibling chain.
    /// </summary>
    [AddComponentMenu("Dexterity/Graph/Providers/Constant Provider")]
    public class ConstantProvider : GraphStateProvider
    {
        [SerializeField, Tooltip("If true, this provider is always active and reports its state.")]
        private bool active = true;

        protected override bool ComputeIsActive() => active;

        public bool Active
        {
            get => active;
            set
            {
                if (active == value) return;
                active = value;
                MarkChanged();
            }
        }

        /// <summary>Flips <see cref="Active"/>. Convenient for click-driven toggle wiring.</summary>
        public void Toggle() => Active = !active;
    }
}
