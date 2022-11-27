using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class RectOffsetModifier : ComponentModifier<RectTransform>, ISupportValueFreeze, ISupportPropertyFreeze
    {
        public Vector2 baseMinOffset;
        public Vector2 baseMaxOffset;

        [Serializable]
        public class Property : PropertyBase
        {
            // custom params
            public Vector2 minOffset;
            public Vector2 maxOffset;
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

            Vector2 minOffset = baseMinOffset;
            Vector2 maxOffset = baseMaxOffset;
            foreach (var kv in transitionState)
            {
                var property = GetProperty(kv.Key) as Property;
                var value = kv.Value;

                minOffset += Vector2.Lerp(Vector2.zero, property.minOffset, value);
                maxOffset += Vector2.Lerp(Vector2.zero, property.maxOffset, value);
            }

            component.offsetMin = minOffset;
            component.offsetMax = maxOffset;

            // update UI layout
            if (updateParentReference)
                CollectTransformsToUpdate();
            foreach (var rectTransform in _transformsToUpdate)
                LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
        }

        public void FreezeProperty(PropertyBase property)
        {
            if (component == null)
                return;

            var prop = property as Property;
            prop.minOffset = component.offsetMin - baseMinOffset;
            prop.maxOffset = component.offsetMax - baseMaxOffset;
        }

        public void FreezeValue()
        {
            if (component == null)
                return;
                
            baseMinOffset = component.offsetMin;
            baseMaxOffset = component.offsetMax;
        }
    }
}
