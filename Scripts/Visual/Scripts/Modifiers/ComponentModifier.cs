using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    public abstract class ComponentModifier<T> : Modifier where T : Component
    {
        private T _component;
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
        }

        protected void Start()
        {
            CacheComponent();
        }
    }
}
