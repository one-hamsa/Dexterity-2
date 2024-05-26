using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    public class RaycastPriorityGroup : MonoBehaviour, IRaycastPriorityGroup
    {
        [Tooltip("Lower is higher priority, default = 0")]
        public int priority;

        int IRaycastPriorityGroup.priority => priority;
    }
}
