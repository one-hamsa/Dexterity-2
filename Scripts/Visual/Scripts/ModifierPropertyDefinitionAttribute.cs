using System;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ModifierPropertyDefinitionAttribute : PropertyAttribute
    {
        public readonly Type propertyType;
        public readonly string propertyName;

        public ModifierPropertyDefinitionAttribute(Type propertyType)
        {
            this.propertyType = propertyType;
        }
        public ModifierPropertyDefinitionAttribute(string propertyName)
        {
            this.propertyName = propertyName;
        }
    }
}