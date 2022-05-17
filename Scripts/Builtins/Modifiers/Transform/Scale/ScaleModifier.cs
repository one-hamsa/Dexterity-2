using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class ScaleModifier : Modifier, ISupportValueFreeze, ISupportPropertyFreeze
    {
        public float baseScale = 1f;

        [Serializable]
        public class Property : PropertyBase
        {
            // custom params
            public float scale = 1f;
        }

        [Tooltip("Check this flag only if the object may be re-parented in runtime")]
        public bool updateParentReference = false;
        
        List<RectTransform> _transformsToUpdate;

        public override void Awake() {
            base.Awake();

            CollectTransformsToUpdate();
        }

        void CollectTransformsToUpdate() {
            _transformsToUpdate = new List<RectTransform>();
            if (transform is RectTransform rectTransform) {
                _transformsToUpdate.Add(rectTransform);
                foreach (var group in gameObject.GetComponentsInParent<LayoutGroup>())
                    if (group.transform is RectTransform parentRectTransform)
                        _transformsToUpdate.Add(parentRectTransform);
            }
        }

        public override void Update()
        {
            base.Update();

            if (!transitionChanged)
                return;

            var scale = Vector3.zero;
            foreach (var kv in transitionState)
            {
                var property = GetProperty(kv.Key) as Property;
                var value = kv.Value;

                scale += Vector3.one * baseScale * property.scale * value;
            }

            transform.localScale = scale;
            
            // update UI layout
            if (updateParentReference)
                CollectTransformsToUpdate();
            foreach (var rectTransform in _transformsToUpdate)
                LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
        }

        public void FreezeValue()
        {
            baseScale = transform.localScale.x;
        }
        public void FreezeProperty(PropertyBase property)
        {
            var prop = property as Property;
            prop.scale = transform.localScale.x / baseScale;
        } 
    }
}
