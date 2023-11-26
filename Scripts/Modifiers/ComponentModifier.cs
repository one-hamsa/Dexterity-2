using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    public abstract class ComponentModifier<T> : Modifier where T : Component
    {
        private T _component;
        protected virtual bool createIfNotFound => false;
        
        protected T component {
            get {
                if (_component == null)
                    CacheComponent();
                return _component;
            }
        }

        private void CacheComponent()
        {
            _component = GetComponent<T>();
            if (_component == null && createIfNotFound)
                _component = gameObject.AddComponent<T>();
        }

        protected void Start()
        {
            CacheComponent();
        }
    }
}
