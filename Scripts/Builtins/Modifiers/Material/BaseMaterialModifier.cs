using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OneHamsa.Dexterity.Visual
{
    /// <summary>
    /// Base class for material modifiers. Takes care of setting up editor transitions
    /// </summary>
    public abstract class BaseMaterialModifier : Modifier
    {
        protected struct SupportedComponentActions {
            public Func<Component, Material> getMaterial;
            public Func<Component, Material> getSharedMaterial;
            public Action<Component, Material> setSharedMaterial;
        }

        private static Dictionary<Type, SupportedComponentActions> supportedComponents = new();
        
        static void AddSupportedComponent<T>(Func<T, Material> getMaterial, Func<T, Material> getSharedMaterial,
            Action<T, Material> setSharedMaterial) where T : Component
        {
            supportedComponents.Add(typeof(T), new SupportedComponentActions {
                getMaterial = (c) => getMaterial((T)c),
                getSharedMaterial = (c) => getSharedMaterial((T)c),
                setSharedMaterial = (c, material) => setSharedMaterial((T)c, material),
            });
        }

        static BaseMaterialModifier()
        {
            AddSupportedComponent<Renderer>(
                c => c.material, 
                c => c.sharedMaterial, 
                (c, m) => c.sharedMaterial = m
                );
            AddSupportedComponent<TextMeshProUGUI>(
                c => c.fontMaterial, 
                c => c.fontSharedMaterial,
                (c, m) => c.fontSharedMaterial = m
                );
            AddSupportedComponent<Image>(
                c => c.material, 
                c => c.material,
                null);
        }
        
        private Material originalMaterial;
        protected Material targetMaterial;
        private (Component component, SupportedComponentActions actions) _cached;

        protected Component component {
            get {
                if (_cached.component == null)
                    CacheComponent();
                return _cached.component;
            }
        }
        protected SupportedComponentActions actions {
            get {
                if (_cached.component == null)
                    CacheComponent();
                return _cached.actions;
            }
        }

        public override void Awake()
        {
            base.Awake();

            #if UNITY_EDITOR
            // support editor transitions
            if (!Application.isPlaying && targetMaterial == null)
            {
                if (actions.setSharedMaterial == null)
                    throw new NotSupportedException("Component does not support shared material");

                originalMaterial = actions.getSharedMaterial(component);
                targetMaterial = new Material(originalMaterial);
                targetMaterial.EnableKeyword("_NORMALMAP");
                targetMaterial.EnableKeyword("_DETAIL_MULX2");
                actions.setSharedMaterial(component, targetMaterial);
            }
            else
            {
                targetMaterial = actions.getMaterial(component);
            }
            #else
            targetMaterial = actions.getMaterial(component);
            #endif
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                actions.setSharedMaterial(component, originalMaterial);
                targetMaterial = null;
            }
            #endif
        }
        

        private void CacheComponent()
        {
            foreach (var kv in supportedComponents)
            {
                var t = kv.Key;
                if (GetComponent(t) != null)
                {
                    var component = GetComponent(t);
                    _cached = (component, kv.Value);
                    return;
                }
            }
            Debug.LogError("No supported component found for ColorModifier", this);
            if (Application.isPlaying)
                enabled = false;
        }

        protected void Start()
        {
            CacheComponent();
        }
    }
}
