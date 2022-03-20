using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    [RequireComponent(typeof(Renderer))]
    public class MaterialLerpModifier : ComponentModifier<Renderer>
    {
        private Material originalMaterial;

        // Start is called before the first frame update [Serializable]

        Material targetMaterial;
        public class Property : PropertyBase
        {
            // custom params
            public Material material;
        }

        public override void Awake()
        {
            base.Awake();

            #if UNITY_EDITOR
            // support editor transitions
            if (targetMaterial == null) {
                originalMaterial = component.sharedMaterial;
                targetMaterial = new Material(originalMaterial);
                component.sharedMaterial = targetMaterial;
            }
            #else
            targetMaterial = component.material;
            #endif
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            #if UNITY_EDITOR
            component.sharedMaterial = originalMaterial;
            #endif
        }

        // Update is called once per frame
        public override void Update()
        {
            base.Update();

            if (!transitionChanged)
                return;
            
            foreach (var kv in transitionState)
            {
                var property = GetProperty(kv.Key) as Property;
                var value = kv.Value;

                var propMaterial = property.material != null ? property.material : targetMaterial;
                targetMaterial.Lerp(targetMaterial, property.material, value);
            }            
        }
    }
}
