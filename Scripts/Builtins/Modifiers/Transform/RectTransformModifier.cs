using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

namespace OneHamsa.Dexterity.Builtins
{
    public class RectTransformModifier : ComponentModifier<RectTransform>, ISupportValueFreeze, ISupportPropertyFreeze
    {
        public Vector2 baseSize;
        public bool syncScale;

        [Serializable]
        public class Property : PropertyBase
        {
            // custom params
            public Vector2 sizeDelta;
        }
        
        [Tooltip("Check this flag only if the object may be re-parented in runtime")]
        public bool updateParentReference = false;

        List<RectTransform> _transformsToUpdate;

        protected override void Awake() {
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
        
        public override void Refresh()
        {
            base.Refresh();

            if (!transitionChanged)
                return;

            Vector2 sizeDelta = baseSize;
            foreach (var kv in transitionState.keyValuePairs)
            {
                var property = GetProperty(kv.Key) as Property;
                var value = kv.Value;

                sizeDelta += Vector2.Lerp(Vector2.zero, property.sizeDelta, value);
            }

            var needsRebuild = false;
            
            if (component.sizeDelta != sizeDelta)
            {
                component.sizeDelta = sizeDelta;
                needsRebuild = true;
            }
            
            if (syncScale)
            {
                var newScale = new Vector3(sizeDelta.x / baseSize.x, sizeDelta.y / baseSize.y, 1);
                if (_transform.localScale != newScale)
                {
                    _transform.localScale = newScale;
                    needsRebuild = true;
                }
            }
            
            if (!needsRebuild)
                return;

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
            prop.sizeDelta = component.sizeDelta - baseSize;
        }

        public void FreezeValue()
        {
            if (component == null)
                return;
                
            baseSize = component.sizeDelta;
        }
    }
}
