using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace OneHamsa.Dexterity.Builtins
{
    [RequireComponent(typeof(ClickListener))]
    public class RecentClickIndicator : MonoBehaviour
    {
        public float recentDuration = 1f;
        
        private ClickListener clickListener;
        private bool hasClickListener;

        private void Awake()
        {
            clickListener = GetComponent<ClickListener>();
            hasClickListener = clickListener != null;
        }
        
        [Preserve]
        public bool IsRecent() => hasClickListener && clickListener.GetTimeSinceClick() < recentDuration;
    }
}