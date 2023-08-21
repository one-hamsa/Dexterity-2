using System;
using UnityEngine;

namespace OneHamsa.Dexterity.Builtins
{
    public class HierarchyListener : MonoBehaviour
    {
        public event Action<Transform> onChildAdded;
        public event Action<Transform> onChildRemoved;
            
        private HierarchyListener parentListener;

        private void OnChildAdded(Transform child)
        {
            if (parentListener != null)
                parentListener.OnChildAdded(child);
            else 
                onChildAdded?.Invoke(child);
        }

        private void OnChildRemoved(Transform child)
        {
            if (parentListener != null)
                parentListener.OnChildRemoved(child);
            else 
                onChildRemoved?.Invoke(child);
        }

        private void OnEnable()
        {
            OnTransformChildrenChanged();
            OnTransformParentChanged();
        }

        private void OnTransformChildrenChanged()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                var childListener = child.GetComponent<HierarchyListener>();
                if (childListener == null)
                {
                    childListener = child.gameObject.AddComponent<HierarchyListener>();
                    childListener.hideFlags = HideFlags.HideInInspector | HideFlags.HideAndDontSave;
                    childListener.OnTransformParentChanged();
                }
            }
        }
        
        private void OnTransformParentChanged()
        {
            if (parentListener != null && parentListener.transform != transform.parent)
                parentListener.OnChildRemoved(transform);

            if (transform.parent != null)
            {
                parentListener = transform.parent.GetComponent<HierarchyListener>();
                if (parentListener != null)
                    parentListener.OnChildAdded(transform);
            }
            else
            {
                parentListener = null;
            }
        }
    }
}