using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

namespace OneHamsa.Dexterity.Builtins
{
    public class RectTransformPivotModifier : ComponentModifier<RectTransform>, ISupportPropertyFreeze
    {
        [Serializable]
        public class Property : PropertyBase
        {
            // custom params
            public Vector2 pivot;
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

            Vector2 pivot = default;
            foreach (var kv in transitionState)
            {
                var property = GetProperty(kv.Key) as Property;
                var value = kv.Value;

                pivot += Vector2.Lerp(Vector2.zero, property.pivot, value);
            }

            component.pivot = pivot;
            
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
            prop.pivot = component.pivot;
        }
    }
}
