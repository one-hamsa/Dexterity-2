using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;
using TMPro;

namespace OneHamsa.Dexterity.Builtins
{
    public class ColorModifier : Modifier, ISupportPropertyFreeze
    {
        private struct SupportedComponentActions {
            public Func<object, Color> getColor;
            public Action<object, Color> setColor;
        }

        private static Dictionary<Type, SupportedComponentActions> supportedComponents = new();
        
        static void AddSupportedComponent<T>(Func<T, Color> getColor, Action<T, Color> setColor)
        {
            supportedComponents.Add(typeof(T), new SupportedComponentActions {
                getColor = (c) => getColor((T)c),
                setColor = (c, color) => setColor((T)c, color),
            });
        }

        static ColorModifier()
        {
            AddSupportedComponent<Image>((c) => c.color, (c, color) => c.color = color);
            AddSupportedComponent<IColorModifierSupport>((c)=> c.GetColor(), (c, color)=> c.SetColor(color));
            AddSupportedComponent<SpriteRenderer>((c) => c.color, (c, color) => c.color = color);
            AddSupportedComponent<TMP_Text>((c) => c.color, (c, color) => c.color = color);
            AddSupportedComponent<CanvasGroup>((c) => new Color(1f, 1f, 1f, c.alpha), (c, color) => c.alpha = color.a);
            AddSupportedComponent<LineRenderer>((c) => new Color(1f, 1f, 1f, c.startColor.a), (c, color) =>
            {
                c.startColor = color;
                c.endColor = color;
            });

        }

        [Serializable]
        public class Property : PropertyBase
        {
            // custom params
            public Color color;
        }

        public override void Refresh()
        {
            base.Refresh();

            if (!transitionChanged)
                return;

            float r = 0, g = 0, b = 0, a = 0;
            foreach (var kv in transitionState.keyValuePairs)
            {
                var property = GetProperty(kv.Key) as Property;
                var value = kv.Value;

                r += property.color.r * value;
                g += property.color.g * value;
                b += property.color.b * value;
                a += property.color.a * value;
            }

            Color prevColor = GetColor();
            Color c = new Color(r, g, b, a);
            if (c != prevColor)
                SetColor(c);
        }

        private void SetColor(Color color)
        {
            actions.setColor(component, color);
        }

        private Color GetColor()
        {
            return actions.getColor(component);
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

            if (Application.isPlaying)
            {
                Debug.LogError($"No supported component found for {GetType().Name}", this);
                enabled = false;
            }
        }

        protected void Start()
        {
            CacheComponent();
        }
        
        #if UNITY_EDITOR
        public override (string, LogType) GetEditorComment()
        {
            if (component is CanvasGroup)
                return ("Only alpha will be applied to CanvasGroup's alpha", LogType.Warning);
            if (component == null) 
                return ("No supported component found", LogType.Error);
            return base.GetEditorComment();
        }
        #endif

        void ISupportPropertyFreeze.FreezeProperty(PropertyBase property)
        {
            if (actions.getColor == null)
                return;
            
            var prop = property as Property;
            
            prop.color = actions.getColor(component);
        }
    }
    public interface IColorModifierSupport
    {
        void SetColor(Color color);
        Color GetColor(); 
    }
}
