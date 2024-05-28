using UnityEngine;

namespace OneHamsa.Dexterity
{
    public class RaycastPriorityGroup : MonoBehaviour, IRaycastPriorityGroup
    {
        [Tooltip("Lower is higher priority, default = 0")]
        public int priority;

        public virtual int GetPriorityForHit(DexterityRaycastHit hit) => priority;
    }
}
