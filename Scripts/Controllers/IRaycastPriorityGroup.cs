using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    public interface IRaycastPriorityGroup
    {
        public int priority { get; }

        public static int GetPriority(Transform t)
        {
            var group = t.GetComponentInParent<IRaycastPriorityGroup>();
            if (group != null)
                return group.priority;
            
            return 0;
        }
    }
}
