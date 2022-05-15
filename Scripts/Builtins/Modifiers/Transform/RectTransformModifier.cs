using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class RectTransformModifier : ComponentModifier<RectTransform>, ISupportValueFreeze, ISupportPropertyFreeze
    {
        public Vector2 baseSize;

        [Serializable]
        public class Property : PropertyBase
        {
            // custom params
            public Vector2 sizeDelta;
        }
        
        [Tooltip("Check this flag only if the object may be re-parented in runtime")]
        public bool updateParentReference = false;

        List<RectTransform> _transformsToUpdate;

        public override void Awake() {
            base.Awake();

            CollectTransformsToUpdate();
        }

        void CollectTransformsToUpdate() {
            _transformsToUpdate = new List<RectTransform> {(RectTransform)transform};
            foreach (var group in gameObject.GetComponentsInParent<LayoutGroup>())
                _transformsToUpdate.Add((RectTransform)group.transform);
        }
        
        public override void Update()
        {
            base.Update();

            if (!transitionChanged)
                return;

            Vector2 sizeDelta = baseSize;
            foreach (var kv in transitionState)
            {
                var property = GetProperty(kv.Key) as Property;
                var value = kv.Value;

                sizeDelta += Vector2.Lerp(Vector2.zero, property.sizeDelta, value);
            }

            component.sizeDelta = sizeDelta;

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
