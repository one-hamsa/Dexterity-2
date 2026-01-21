using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OneHamsa.Dexterity
{
    public class MaterialLerpModifier : BaseMaterialModifier
    {
        [Serializable]
        public class Property : PropertyBase
        {
            // custom params
            public Material material;
        }

        private Dictionary<int, int> intLerps = new();
        private Dictionary<int, float> floatLerps = new();
        private Dictionary<int, Color> colorLerps = new();
        private Dictionary<int, Vector4> vectorLerps = new();

        // Update is called once per frame
        public override void Refresh()
        {
            base.Refresh();

            if (!transitionChanged)
                return;
            
            intLerps.Clear();
            foreach (var (k, v) in initialInts)
                intLerps[k] = v;
            floatLerps.Clear();
            foreach (var (k, v) in initialFloats)
                floatLerps[k] = v;
            colorLerps.Clear();
            foreach (var (k, v) in initialColors)
                colorLerps[k] = v;
            vectorLerps.Clear();
            foreach (var (k, v) in initialVectors)
                vectorLerps[k] = v;
            
            foreach (var kv in transitionState.keyValuePairs)
            {
                var property = (Property)GetProperty(kv.Key);
                var value = kv.Value;
                
                if (property.material == null)
                    continue;

                foreach (var (propId, propType) in propertyTypes)
                {
                    switch (propType)
                    {
                        case ShaderPropertyType.Int:
                            intLerps.TryGetValue(propId, out var intLerp);
                            intLerps[propId] = Mathf.RoundToInt(Mathf.Lerp(intLerp, property.material.GetInt(propId), value));
                            break; 
                        
                        case ShaderPropertyType.Float:
                            floatLerps.TryGetValue(propId, out var floatLerp);
                            floatLerps[propId] = Mathf.Lerp(floatLerp, property.material.GetFloat(propId), value);
                            break;
                        
                        case ShaderPropertyType.Color:
                            colorLerps.TryGetValue(propId, out var colorLerp);
                            colorLerps[propId] = Color.Lerp(colorLerp, property.material.GetColor(propId), value);
                            break;
                        
                        case ShaderPropertyType.Vector:
                            vectorLerps.TryGetValue(propId, out var vectorLerp);
                            vectorLerps[propId] = Vector4.Lerp(vectorLerp, property.material.GetVector(propId), value);
                            break;
                    }
                }
                
                foreach (var (propId, propType) in propertyTypes)
                {
                    switch (propType)
                    {
                        case ShaderPropertyType.Int:
                            SetInt(propId, intLerps[propId]);
                            break; 
                        
                        case ShaderPropertyType.Float:
                            SetFloat(propId, floatLerps[propId]);
                            break;
                        
                        case ShaderPropertyType.Color:
                            SetColor(propId, colorLerps[propId]);
                            break;
                        
                        case ShaderPropertyType.Vector:
                            SetVector(propId, vectorLerps[propId]);
                            break;
                    }
                }
            }
        }
    }
}
