using UnityEngine;
using TMPro;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    [RequireComponent(typeof(TMP_Text))]
    public class TMProColorModifier : ColorModifier, ISupportPropertyFreeze
    {
        TMP_Text GetTMP_Text() {
            if (_tmpro == null)
                _tmpro = GetComponent<TMP_Text>();
            return _tmpro;
        }
        TMP_Text _tmpro;
        protected void Start()
        {
            // cache
            GetTMP_Text();
        }

        protected override void SetColor(Color color) => GetTMP_Text().color = color;

        public void FreezeProperty(PropertyBase property)
        {
            var prop = property as Property;
            prop.color = GetTMP_Text().color;
        }
    }
}
