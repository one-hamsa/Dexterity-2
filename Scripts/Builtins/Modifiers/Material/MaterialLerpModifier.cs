using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    public class MaterialLerpModifier : BaseMaterialModifier, ISupportPropertyFreeze
    {
        public class Property : PropertyBase
        {
            // custom params
            public Material material;
        }

        // Update is called once per frame
        public override void Refresh()
        {
            base.Refresh();

            if (!transitionChanged)
                return;
            
            foreach (var kv in transitionState)
            {
                var property = GetProperty(kv.Key) as Property;
                var value = kv.Value;

                var propMaterial = property.material != null ? property.material : targetMaterial;
                targetMaterial.Lerp(targetMaterial, propMaterial, value);
            }
        }

        void ISupportPropertyFreeze.FreezeProperty(PropertyBase property)
        {
            var prop = property as Property;

            prop.material = actions.getSharedMaterial(component);
        }
    }
}
