using System.Collections.Generic;
using UnityEngine;
using System;

namespace OneHamsa.Dexterity.Builtins
{
    public class InteractivityModifier : Modifier
    {
        [Tooltip("If true, will also control colliders under nested InteractivityModifiers")]
        public bool recursive = true;
        
        private List<Collider> cachedColliders = new();
        public override bool animatableInEditor => false;

        private InteractivityModifier parent;
        private bool dirty;
        
        private event Action onInteractiveChanged;

        [Serializable]
        public class Property : PropertyBase
        {
            // custom params
            public bool interactive;
        }

        protected override void OnEnable()
        {
            dirty = true;
            base.OnEnable();
            
            var node = GetNode();
            if (node != null)
            {
                node.onChildNodesChanged += SetDirty;
            }

            var parent = transform.parent != null
                ? transform.parent.GetComponentInParent<InteractivityModifier>()
                : null;
            
            if (parent != null && parent.recursive)
            {
                this.parent = parent;
                parent.SetDirty();
                parent.onInteractiveChanged += RefreshInteractive;
            }
            else
                this.parent = null;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            
            var node = GetNode();
            if (node != null)
            {
                node.onChildNodesChanged -= SetDirty;
            }

            if (parent != null)
            {
                parent.onInteractiveChanged -= RefreshInteractive;
                parent.SetDirty();
            }

            RefreshInteractive();
        }

        public void SetDirty() => dirty = true;

        private void LateUpdate()
        {
            if (dirty)
            {
                dirty = false;
                
                // collect colliders
                RefreshTrackedColliders();
                // update colliders
                RefreshInteractive();
            }
        }

        private void RefreshInteractive()
        {
            PruneStaleColliders();
            
            var interactive = GetMyInteractive();
            
            // if relevant, consider parent too
            if (parent != null)
                interactive &= parent.GetMyInteractive();

            foreach (var c in cachedColliders)
                c.enabled = interactive;
            
            onInteractiveChanged?.Invoke();
        }

        private void RefreshTrackedColliders()
        {
            cachedColliders.Clear();
            if (recursive)
            {
                GetComponentsInChildren(true, cachedColliders);
                
                // clear out any colliders that are under other InteractivityModifiers
                for (var i = cachedColliders.Count - 1; i >= 0; i--)
                {
                    var parent = cachedColliders[i].GetComponentInParent<InteractivityModifier>();
                    if (parent != this && parent.enabled)
                        cachedColliders.RemoveAt(i);
                }
            }
            else
                GetComponents(cachedColliders);
        }

        public override void HandleStateChange(int oldState, int newState) {
            base.HandleStateChange(oldState, newState);
            RefreshInteractive();
        }

        private bool GetMyInteractive()
        {
            if (parent != null && !parent.GetMyInteractive())
                return false;

            if (!isActiveAndEnabled)
                // ignore
                return true;
            
            return ((Property)GetProperty(GetNodeActiveStateWithDelay())).interactive;
        }

        /// <summary>
        /// Removes colliders that are destroyed or no longer children of this transform
        /// </summary>
        private void PruneStaleColliders()
        {
            // in-place cleanup
            for (var i = cachedColliders.Count - 1; i >= 0; i--) 
            {
                if (cachedColliders[i] == null || !cachedColliders[i].transform.IsChildOf(transform))
                    cachedColliders.RemoveAt(i);
            }
        }
        
        public override void Refresh()
        {
	        base.Refresh();
	        // always mark as didn't change
	        transitionChanged = false;
        }
    }
}
