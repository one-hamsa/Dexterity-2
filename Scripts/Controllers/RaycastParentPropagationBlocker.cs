using UnityEngine;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// Stop propagation of raycast hits to parent objects.
    /// <see cref="IBlockRaycastParentPropagation"/>
    /// <see cref="OneHamsa.Dexterity.Builtins.RaycastController"/> && <see cref="IRaycastController"/>
    /// </summary>
    [DisallowMultipleComponent]
    public class RaycastParentPropagationBlocker : MonoBehaviour, IBlockRaycastParentPropagation
    {
    }
}
