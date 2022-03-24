using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;
using TMPro;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class ColorModifier : Modifier, ISupportPropertyFreeze
    {
        private struct SupportedComponentActions {
            public Func<Component, Color> getColor;
            public Action<Component, Color> setColor;
        }

        private static Dictionary<Type, SupportedComponentActions> supportedComponents 
        = new Dictionary<Type, SupportedComponentActions>();
        
        static void AddSupportedComponent<T>(Func<T, Color> getColor, Action<T, Color> setColor) where T : Component
        {
            supportedComponents.Add(typeof(T), new SupportedComponentActions {
                getColor = (c) => getColor((T)c),
                setColor = (c, color) => setColor((T)c, color),
            });
        }

        static ColorModifier()
        {
            AddSupportedComponent<Image>((c) => c.color, (c, color) => c.color = color);
            AddSupportedComponent<SpriteRenderer>((c) => c.color, (c, color) => c.color = color);
            AddSupportedComponent<TMP_Text>((c) => c.color, (c, color) => c.color = color);
            AddSupportedComponent<CanvasGroup>((c) => new Color(1f, 1f, 1f, c.alpha), (c, color) => c.alpha = color.a);
        }

        [Serializable]
        public class Property : PropertyBase
        {
            // custom params
            public Color color;
        }

        public override void Update()
        {
            base.Update();

            if (!transitionChanged)
                return;

            float r = 0, g = 0, b = 0, a = 0;
            foreach (var kv in transitionState)
            {
                var property = GetProperty(kv.Key) as Property;
                var value = kv.Value;

                r += property.color.r * value;
                g += property.color.g * value;
                b += property.color.b * value;
                a += property.color.a * value;
            }

            SetColor(new Color(r, g, b, a));
        }

        private void SetColor(Color color)
        {
            actions.setColor(component, color);
        }

        private (Component component, SupportedComponentActions actions) _cached;

        protected Component component {
            get {
                if (_cached.component == null)
                    CacheComponent();
                return _cached.component;
            }
        }
        private SupportedComponentActions actions {
            get {
                if (_cached.component == null)
                    CacheComponent();
                return _cached.actions;
            }
        }

        private void CacheComponent()
        {
            foreach (var kv in supportedComponents)
            {
                var t = kv.Key;
                if (GetComponent(t) != null)
                {
                    var component = GetComponent(t);
                    _cached = (component, kv.Value);
                    return;
                }
            }
            Debug.LogError("No supported component found for ColorModifier", this);
            if (Application.isPlaying)
                enabled = false;
        }

        protected void Start()
        {
            CacheComponent();
        }

        void ISupportPropertyFreeze.FreezeProperty(PropertyBase property)
        {
            var prop = property as Property;
            
            prop.color = actions.getColor(component);
        }
    }
}
