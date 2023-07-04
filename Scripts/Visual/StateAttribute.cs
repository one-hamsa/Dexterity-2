using UnityEngine;

namespace OneHamsa.Dexterity
{
    public class StateAttribute : PropertyAttribute
    {
        public readonly bool allowEmpty;
        public readonly string objectFieldName;
        public StateAttribute(bool allowEmpty = false, string objectFieldName = null)
        {
            this.allowEmpty = allowEmpty;
            this.objectFieldName = objectFieldName;
        }
    }
}