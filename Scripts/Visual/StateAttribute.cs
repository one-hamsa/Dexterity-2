using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    public class StateAttribute : PropertyAttribute
    {
        public readonly bool allowEmpty;
        public StateAttribute(bool allowEmpty = false)
        {
            this.allowEmpty = allowEmpty;
        }
    }
}