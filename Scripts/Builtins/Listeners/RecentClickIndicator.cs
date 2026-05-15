using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
    public class RecentClickIndicator : MonoBehaviour
    {
        public float recentDuration = 1f;

        private BaseClickListener clickListener;
        private bool hasBaseClickListener;

        private void Awake()
        {
            clickListener = GetComponent<BaseClickListener>();
            hasBaseClickListener = clickListener != null;
            if (!hasBaseClickListener)
                Debug.LogError($"RecentClickIndicator on {name} needs a sibling BaseClickListener " +
                               "(FieldNodeClickListener or HierarchyNodeClickListener) to time clicks against.", this);
        }
        
        [Preserve]
        public bool IsRecent() => hasBaseClickListener && clickListener.GetTimeSinceClick() < recentDuration;
    }
}