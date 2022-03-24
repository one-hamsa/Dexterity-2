using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    /// <summary>
    /// Base class for material modifiers. Takes care of setting up editor transitions
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public abstract class BaseMaterialModifier : ComponentModifier<Renderer>
    {
        private Material originalMaterial;
        protected Material targetMaterial;

        public override void Awake()
        {
            base.Awake();

            #if UNITY_EDITOR
            // support editor transitions
            if (targetMaterial == null) {
                originalMaterial = component.sharedMaterial;
                targetMaterial = new Material(originalMaterial);
                targetMaterial.EnableKeyword("_NORMALMAP");
                targetMaterial.EnableKeyword("_DETAIL_MULX2");
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
            targetMaterial = null;
            #endif
        }
    }
}
