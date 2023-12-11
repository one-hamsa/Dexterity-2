using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// Base class for material modifiers. Takes care of setting up editor transitions
    /// </summary>
    public abstract class BaseMaterialModifier : Modifier, IMaterialModifier
    {
        protected enum MaterialType
        {
            Graphic,
            Renderer
        }
        
        private static Dictionary<Type, MaterialType> supportedComponents = new();
        
        [HideInInspector] public bool enableEditorMaterialAnimations;

        private bool _cached;
        private Component _component;
        private MaterialType _materialType;
        private Dictionary<int, ShaderPropertyType> _propertyTypes = new();
        private MaterialPropertyBlock propertyBlock;  // shared across all materials
        private Graphic graphic;
        
        protected Dictionary<int, int> initialInts = new();
        protected Dictionary<int, float> initialFloats = new();
        protected Dictionary<int, Color> initialColors = new();
        protected Dictionary<int, Vector4> initialVectors = new();
        
        private Dictionary<int, int> intOverrides = new();
        private Dictionary<int, float> floatOverrides = new();
        private Dictionary<int, Vector4> vectorOverrides = new();
        private Dictionary<int, Color> colorOverrides = new();
        private Dictionary<int, Texture> textureOverrides = new();
        private Dictionary<int, Matrix4x4> matrixOverrides = new();
        
        private Material modifiedMaterial;

        static void AddSupportedComponent<T>(MaterialType materialType) where T : Component
        {
            supportedComponents.Add(typeof(T), materialType);
        }

        static BaseMaterialModifier()
        {
            AddSupportedComponent<Renderer>(MaterialType.Renderer);
            AddSupportedComponent<TMP_Text>(MaterialType.Graphic);
            AddSupportedComponent<Image>(MaterialType.Graphic);
        }
        
        protected override void OnEnable()
        {
            base.OnEnable();
            SetMaterialDirty();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (Application.isPlaying)
            {
                Destroy(modifiedMaterial);
            }
            else
            {
                DestroyImmediate(modifiedMaterial);
            }
            SetMaterialDirty();
        }

        protected void Start()
        {
            if (!_cached)
                CacheComponent();
        }

        private void OnDestroy()
        {
            if (modifiedMaterial == null) 
                return;
            
            if (Application.isPlaying)
                Destroy(modifiedMaterial);
            else
                DestroyImmediate(modifiedMaterial);
        }

        public override void PrepareTransition_Editor(string initialState, string targetState)
        {
            enableEditorMaterialAnimations = true;
            SetMaterialDirty();
            
            base.PrepareTransition_Editor(initialState, targetState);
            CacheComponent();
        }

        protected Component component
        {
            get
            {
                if (!_cached)
                    CacheComponent(); 
                return _component;
            }
        }
        protected MaterialType materialType
        {
            get
            {
                if (!_cached)
                    CacheComponent(); 
                return _materialType;
            }
        }
        protected Dictionary<int, ShaderPropertyType> propertyTypes
        {
            get
            {
                if (!_cached)
                    CacheComponent(); 
                return _propertyTypes;
            }
        }

        protected void CacheComponent()
        {
            foreach (var kv in supportedComponents)
            {
                var t = kv.Key;
                if (GetComponent(t) != null)
                {
                    _component = GetComponent(t);
                    _materialType = kv.Value;
                    _cached = true;
                    switch (_materialType)
                    {
                        case MaterialType.Graphic:
                            GetMaterialProperties(new [] { ((Graphic)component).materialForRendering });
                            break;
                        
                        case MaterialType.Renderer:
                            GetMaterialProperties(((Renderer)component).sharedMaterials);
                            break;
                    }

                    return;
                }
            }

            if (Application.IsPlaying(this))
            {
                Debug.LogError($"No supported component found for {GetType().Name}", this);
                enabled = false;
            }
        }

        private void GetMaterialProperties(Material[] materials)
        {
            propertyTypes.Clear();
            foreach (var material in materials)
            {
                var shader = material.shader;
                var count = shader.GetPropertyCount();
                for (int i = 0; i < count; i++)
                {
                    var propertyNameId = shader.GetPropertyNameId(i);
                    var propertyType = shader.GetPropertyType(i);
                    propertyTypes[propertyNameId] = propertyType;
                    switch (propertyType)
                    {
                        case ShaderPropertyType.Int:
                            initialInts[propertyNameId] = material.GetInt(propertyNameId);
                            break; 
                        
                        case ShaderPropertyType.Float:
                            initialFloats[propertyNameId] = material.GetFloat(propertyNameId);
                            break;
                        
                        case ShaderPropertyType.Color:
                            initialColors[propertyNameId] = material.GetColor(propertyNameId);
                            break;
                        
                        case ShaderPropertyType.Vector:
                            initialVectors[propertyNameId] = material.GetVector(propertyNameId);
                            break;
                    }
                }
            }
        }

        public void SetMaterialDirty()
        {
            if (component == null)
                return;

            switch (materialType)
            {
                case MaterialType.Graphic:
                    if ((Application.isPlaying && graphic != null) || TryGetComponent(out graphic))
                        graphic.SetMaterialDirty();

                    break;
                
                case MaterialType.Renderer:
                    GetPropertyBlock();
                    if (!overrideMaterial)
                    {
                        propertyBlock.Clear();
                        ((Renderer)component).SetPropertyBlock(propertyBlock);
                        return;
                    }
                    
                    foreach (var kv in intOverrides)
                    {
                        propertyBlock.SetInt(kv.Key, kv.Value);
                    }
                    
                    foreach (var kv in floatOverrides)
                    {
                        propertyBlock.SetFloat(kv.Key, kv.Value);
                    }
                    
                    foreach (var kv in vectorOverrides)
                    {
                        propertyBlock.SetVector(kv.Key, kv.Value);
                    }
                    
                    foreach (var kv in colorOverrides)
                    {
                        propertyBlock.SetColor(kv.Key, kv.Value);
                    }
                    
                    foreach (var kv in textureOverrides)
                    {
                        propertyBlock.SetTexture(kv.Key, kv.Value);
                    }
                    
                    foreach (var kv in matrixOverrides)
                    {
                        propertyBlock.SetMatrix(kv.Key, kv.Value);
                    }
                    ((Renderer)component).SetPropertyBlock(propertyBlock);
                    break;
            }
        }

        private void GetPropertyBlock()
        {
            propertyBlock ??= new();
            ((Renderer)component).GetPropertyBlock(propertyBlock);
        }

        public Material GetModifiedMaterial(Material baseMaterial)
        {
            // Return the base material if invalid or if this component is disabled
            if (!enabled || baseMaterial == null || !overrideMaterial)
                return baseMaterial;
            
            if (component == null || materialType != MaterialType.Graphic)
                return baseMaterial;

            if (modifiedMaterial == null)
            {
                // Create a child material of the original
                modifiedMaterial = new(baseMaterial.shader)
                {
                    // Set a new name, to warn about editor modifications
                    name = $"{baseMaterial.name} OVERRIDE",
                    hideFlags = HideFlags.HideAndDontSave | HideFlags.NotEditable
                };
            }
#if UNITY_2022_1_OR_NEWER && UNITY_EDITOR
            modifiedMaterial.parent = baseMaterial;
#endif
            modifiedMaterial.CopyPropertiesFromMaterial(baseMaterial);

            foreach (var kv in intOverrides)
            {
                modifiedMaterial.SetInt(kv.Key, kv.Value);
            }
            
            foreach (var kv in floatOverrides)
            {
                modifiedMaterial.SetFloat(kv.Key, kv.Value);
            }
            
            foreach (var kv in vectorOverrides)
            {
                modifiedMaterial.SetVector(kv.Key, kv.Value);
            }
            
            foreach (var kv in colorOverrides)
            {
                modifiedMaterial.SetColor(kv.Key, kv.Value);
            }
            
            foreach (var kv in textureOverrides)
            {
                modifiedMaterial.SetTexture(kv.Key, kv.Value);
            }
            
            foreach (var kv in matrixOverrides)
            {
                modifiedMaterial.SetMatrix(kv.Key, kv.Value);
            }

            // Return the child material
            return modifiedMaterial;
        }

        private bool overrideMaterial => Application.IsPlaying(this) || enableEditorMaterialAnimations;

        protected int GetInt(int id)
        {
            if (intOverrides.TryGetValue(id, out var value))
                return value;
           
            switch (materialType)
            {
                case MaterialType.Graphic:
                    if (graphic == null)
                        graphic = GetComponent<Graphic>();
                    return graphic.materialForRendering.GetInt(id);
                
                case MaterialType.Renderer:
                    GetPropertyBlock();
                    if (!propertyBlock.isEmpty)
                        return propertyBlock.GetInt(id);
                    
                    return ((Renderer)component).sharedMaterial.GetInt(id);
            }

            return default;
        }
        
        protected float GetFloat(int id)
        {
            if (floatOverrides.TryGetValue(id, out var value))
                return value;
           
            switch (materialType)
            {
                case MaterialType.Graphic:
                    if (graphic == null)
                        graphic = GetComponent<Graphic>();
                    
                    if (!graphic.materialForRendering.HasFloat(id))
                        return 0;
                    return graphic.materialForRendering.GetFloat(id);
                
                case MaterialType.Renderer:
                    GetPropertyBlock();
                    if (!propertyBlock.isEmpty)
                        return propertyBlock.GetFloat(id);
                    
                    if (!((Renderer)component).sharedMaterial.HasFloat(id))
                        return 0;
                    return ((Renderer)component).sharedMaterial.GetFloat(id);
            }

            return default;
        }
        
        protected Vector4 GetVector(int id)
        {
            if (vectorOverrides.TryGetValue(id, out var value))
                return value;
           
            switch (materialType)
            {
                case MaterialType.Graphic:
                    if (graphic == null)
                        graphic = GetComponent<Graphic>();
                    
                    if (!graphic.materialForRendering.HasVector(id))
                        return default;
                    return graphic.materialForRendering.GetVector(id);
                
                case MaterialType.Renderer:
                    GetPropertyBlock();
                    if (!propertyBlock.isEmpty)
                        return propertyBlock.GetVector(id);
                    
                    if (!((Renderer)component).sharedMaterial.HasVector(id))
                        return default;
                    return ((Renderer)component).sharedMaterial.GetVector(id);
            }

            return default;
        }
        
        protected Color GetColor(int id)
        {
            if (colorOverrides.TryGetValue(id, out var value))
                return value;
           
            switch (materialType)
            {
                case MaterialType.Graphic:
                    if (graphic == null)
                        graphic = GetComponent<Graphic>();
                    
                    if (!graphic.materialForRendering.HasColor(id))
                        return default;
                    return graphic.materialForRendering.GetColor(id);
                
                case MaterialType.Renderer:
                    GetPropertyBlock();
                    if (!propertyBlock.isEmpty)
                        return propertyBlock.GetColor(id);
                    
                    if (!((Renderer)component).sharedMaterial.HasColor(id))
                        return default;
                    return ((Renderer)component).sharedMaterial.GetColor(id);
            }

            return default;
        }
        
        protected Texture GetTexture(int id)
        {
            if (textureOverrides.TryGetValue(id, out var value))
                return value;
           
            switch (materialType)
            {
                case MaterialType.Graphic:
                    if (graphic == null)
                        graphic = GetComponent<Graphic>();
                    
                    if (!graphic.materialForRendering.HasTexture(id))
                        return default;
                    return graphic.materialForRendering.GetTexture(id);
                
                case MaterialType.Renderer:
                    GetPropertyBlock();
                    if (!propertyBlock.isEmpty)
                        return propertyBlock.GetTexture(id);
                    
                    if (!((Renderer)component).sharedMaterial.HasTexture(id))
                        return default;
                    return ((Renderer)component).sharedMaterial.GetTexture(id);
            }

            return default;
        }
        
        protected Matrix4x4 GetMatrix(int id)
        {
            if (matrixOverrides.TryGetValue(id, out var value))
                return value;
           
            switch (materialType)
            {
                case MaterialType.Graphic:
                    if (graphic == null)
                        graphic = GetComponent<Graphic>();
                    
                    if (!graphic.materialForRendering.HasMatrix(id))
                        return default;
                    return graphic.materialForRendering.GetMatrix(id);
                
                case MaterialType.Renderer:
                    GetPropertyBlock();
                    if (!propertyBlock.isEmpty)
                        return propertyBlock.GetMatrix(id);
                    
                    if (!((Renderer)component).sharedMaterial.HasMatrix(id))
                        return default;
                    return ((Renderer)component).sharedMaterial.GetMatrix(id);
            }

            return default;
        }
        
        protected void SetInt(int id, int value)
        {
            intOverrides[id] = value;
            SetMaterialDirty();
        }
        protected void SetFloat(int id, float value)
        {
            floatOverrides[id] = value;
            SetMaterialDirty();
        }
        
        protected void SetVector(int id, Vector4 value)
        {
            vectorOverrides[id] = value;
            SetMaterialDirty();
        }
        
        protected void SetColor(int id, Color value)
        {
            colorOverrides[id] = value;
            SetMaterialDirty();
        }
        
        protected void SetTexture(int id, Texture value)
        {
            textureOverrides[id] = value;
            SetMaterialDirty();
        }
        
        protected void SetMatrix(int id, Matrix4x4 value)
        {
            matrixOverrides[id] = value;
            SetMaterialDirty();
        }

        #if UNITY_EDITOR
        public override (string, LogType) GetEditorComment()
        {
            if (component == null) 
                return ("No supported component found", LogType.Error);
            return base.GetEditorComment();
        }
        #endif

        protected override void InitializedCachedData()
        {
            base.InitializedCachedData();
            if (!_cached)
                CacheComponent();
        }
    }
}
