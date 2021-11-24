using UnityEngine;
using TMPro;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    [RequireComponent(typeof(TMP_Text))]
    public class TMProColorModifier : ColorModifier, ISupportPropertyFreeze
    {
        TMP_Text _tmpro;
        protected void Start()
        {
            _tmpro = GetComponent<TMP_Text>();
        }

        protected override void SetColor(Color color) => _tmpro.color = color;

        public void FreezeProperty(PropertyBase property)
        {
            var prop = property as Property;
            var tmpro = GetComponent<TMP_Text>();
            prop.color = tmpro.color;
        }
    }
}
