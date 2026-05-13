using UnityEngine;

namespace OneHamsa.Dexterity.Builtins
{
    /// <summary>
    /// HierarchyNode equivalent of <see cref="ConstantField"/>.
    /// Reports its state unconditionally while <see cref="active"/> is true.
    /// Useful as a terminal fallback at the end of a sibling chain.
    /// </summary>
    [AddComponentMenu("Dexterity/Hierarchy/Providers/Constant Provider")]
    public class ConstantProvider : HierarchyStateProvider
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
    }
}
