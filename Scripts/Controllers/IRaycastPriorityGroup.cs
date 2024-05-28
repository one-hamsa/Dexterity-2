using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    public interface IRaycastPriorityGroup
    {
        public const int ABORT_PRIORITY = 99999;
        
        public int GetPriorityForHit(DexterityRaycastHit hit);

        public static int GetPriority(DexterityRaycastHit hit)
        {
            var group = hit.transform.GetComponentInParent<IRaycastPriorityGroup>();
            if (group != null)
                return group.GetPriorityForHit(hit);
            
            return 0;
        }
    }
}
