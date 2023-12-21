using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    public abstract class ComponentModifier<T> : Modifier where T : Component
    {
        private T _component;
        protected virtual bool createIfNotFound => false;

        private bool _cached;
        
        protected T component {
            get {
                if (!_cached)
                    CacheComponent();
                return _component;
            }
        }

        private void CacheComponent()
        {
            _component = GetComponent<T>();
            if (_component == null && createIfNotFound)
                _component = gameObject.AddComponent<T>();

            _cached = true;
        }

        protected void Start()
        {
            if (!_cached)
                CacheComponent();
        }

        protected override void InitializeCacheData()
        {
            base.InitializeCacheData();
            if (!_cached)
                CacheComponent();
        }
    }
}
