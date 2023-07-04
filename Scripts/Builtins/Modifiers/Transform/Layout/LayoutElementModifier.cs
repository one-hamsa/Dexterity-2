using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;

namespace OneHamsa.Dexterity.Builtins
{
    public class LayoutElementModifier : ComponentModifier<LayoutElement>, ISupportPropertyFreeze
    {
        [Serializable]
        public class Property : PropertyBase
        {
            // custom params
            public float preferredWidth;
            public float preferredHeight;
            public float flexibleWidth;
            public float flexibleHeight;
        }


        public override void Refresh()
        {
            base.Refresh();

            if (!transitionChanged)
                return;

            var preferredWidth = 0f;
            var preferredHeight = 0f;
            var flexibleWidth = 0f;
            var flexibleHeight = 0f;

            foreach (var kv in transitionState)
            {
                var property = GetProperty(kv.Key) as Property;
                var value = kv.Value;

                preferredWidth += Mathf.Lerp(0, property.preferredWidth, value);
                preferredHeight += Mathf.Lerp(0, property.preferredHeight, value);
                flexibleWidth += Mathf.Lerp(0, property.flexibleWidth, value);
                flexibleHeight += Mathf.Lerp(0, property.flexibleHeight, value);
            }

            component.preferredWidth = preferredWidth;
            component.preferredHeight = preferredHeight;
            component.flexibleWidth = flexibleWidth;
            component.flexibleHeight = flexibleHeight;
        }

        public void FreezeProperty(PropertyBase property)
        {
            var prop = (Property)property;
            prop.preferredWidth = component.preferredWidth;
            prop.preferredHeight = component.preferredHeight;
            prop.flexibleWidth = component.flexibleWidth;
            prop.flexibleHeight = component.flexibleHeight;
        } 
    }
}
