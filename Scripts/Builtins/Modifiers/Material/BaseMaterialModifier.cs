using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// Base class for material modifiers. Takes care of setting up editor transitions
    /// </summary>
    public abstract class BaseMaterialModifier : Modifier
    {
        protected struct SupportedComponentActions {
            public Func<Component, (Material material, bool created)> getMaterial;
            public Func<Component, Material> getSharedMaterial;
            public Action<Component, Material> setSharedMaterial;
        }

        private static Dictionary<Type, SupportedComponentActions> supportedComponents = new();
        
        static void AddSupportedComponent<T>(Func<T, (Material material, bool created)> getMaterial, Func<T, Material> getSharedMaterial,
            Action<T, Material> setSharedMaterial) where T : Component
        {
            supportedComponents.Add(typeof(T), new SupportedComponentActions {
                getMaterial = (c) => getMaterial((T)c),
                getSharedMaterial = (c) => getSharedMaterial((T)c),
                setSharedMaterial =  (c, material) => setSharedMaterial((T)c, material),
            });
        }

        static BaseMaterialModifier()
        {
            AddSupportedComponent<Renderer>(
                c =>
                {
                    return !Application.isPlaying 
                        // this is handled by the AnimationEditorContext
                        ? (c.sharedMaterial, false) 
                        : (c.material, false);
                }, 
                c => c.sharedMaterial, 
                (c, m) => c.sharedMaterial = m
                );
            AddSupportedComponent<TextMeshProUGUI>(
                c => (c.fontMaterial, false), 
                c => c.fontSharedMaterial,
                (c, m) => c.fontSharedMaterial = m
                );
            AddSupportedComponent<Image>(
                // for image component, material is actually sharedMaterial (it won't create a new one for us)
                c =>
                {
                    var material = new Material(c.material);
                    c.material = material;
                    return (material, true);
                }, 
                c => c.material,
                (c, m) => c.material = m);
        }
        
        private Material originalMaterial;
        protected Material targetMaterial;
        private (Component component, SupportedComponentActions actions) _cached;
        private bool shouldDestroyTargetMaterial;

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
            (targetMaterial, shouldDestroyTargetMaterial) = actions.getMaterial(component);
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
        public void OnDestroy()
        {
            if (targetMaterial != null && shouldDestroyTargetMaterial)
            {
                Destroy(targetMaterial);
                targetMaterial = null;
            }
        }
        
        #if UNITY_EDITOR
        public class MaterialModifierEditorAnimationContext : EditorAnimationContext
        {
            private GameObject newGo;
            private readonly Material[] materials;
            private readonly Component originalComponent;

            public MaterialModifierEditorAnimationContext(BaseMaterialModifier modifier) : base(modifier)
            {
                var go = modifier.gameObject;
                originalComponent = modifier.component;
                
                newGo = Instantiate(go, go.transform.parent);
                DisableOriginalComponent();
                newGo.transform.localPosition = go.transform.localPosition;
                newGo.transform.localRotation = go.transform.localRotation;
                newGo.transform.localScale = go.transform.localScale;
                newGo.hideFlags = HideFlags.HideAndDontSave;
                newGo.name = $"[Editor] {go.name}";

                var tempModifier = newGo.GetComponent<BaseMaterialModifier>();
                this.modifier = tempModifier;
                tempModifier.CacheComponent();
                
                var renderer = newGo.GetComponent<Renderer>();
                if (renderer != null)
                {
                    materials = UnityEditorInternal.InternalEditorUtility.InstantiateMaterialsInEditMode(renderer);
                    foreach (var material in materials)
                    {
                        material.name = $"[Editor] {material.name}";
                        material.EnableKeyword("_NORMALMAP");
                        material.EnableKeyword("_DETAIL_MULX2");
                    }
                    renderer.sharedMaterials = materials;
                }
                
                UnityEditor.SceneView.RepaintAll();
            }

            private void DisableOriginalComponent()
            {
                switch (originalComponent)
                {
                    case Renderer renderer:
                        renderer.enabled = false;
                        break;
                    case MaskableGraphic graphic:
                        graphic.enabled = false;
                        break;
                    case MonoBehaviour monoBehaviour:
                        monoBehaviour.enabled = false;
                        break;
                    default:
                        throw new Exception($"Unsupported component type {originalComponent.GetType()}");
                }
            }
            
            private void EnableOriginalComponent()
            {
                switch (originalComponent)
                {
                    case Renderer renderer:
                        renderer.enabled = true;
                        break;
                    case MaskableGraphic graphic:
                        graphic.enabled = true;
                        break;
                    case MonoBehaviour monoBehaviour:
                        monoBehaviour.enabled = true;
                        break;
                    default:
                        throw new Exception($"Unsupported component type {originalComponent.GetType()}");
                }
            }

            public override void Dispose()
            {
                base.Dispose();
                
                if (originalComponent != null) 
                    EnableOriginalComponent();

                if (materials != null)
                {
                    foreach (var material in materials)
                    {
                        DestroyImmediate(material);
                    }
                }
               
                if (newGo != null)
                {
                    DestroyImmediate(newGo);
                    newGo = null;
                }
            }
        }

        public override EditorAnimationContext GetEditorAnimationContext() 
            => new MaterialModifierEditorAnimationContext(this);
#endif
    }
}
