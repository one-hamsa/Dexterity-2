using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    [RequireComponent(typeof(Renderer))]
    public class MaterialLerpModifier : Modifier
    {
        // Start is called before the first frame update [Serializable]

        Material targetMaterial;
        public class Property : PropertyBase
        {
            // custom params
            public Material material;
        }
        void Start()
        {
            targetMaterial = GetComponent<Renderer>().material;
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

                targetMaterial.Lerp(targetMaterial, property.material, value);
            }            
        }
    }
}
