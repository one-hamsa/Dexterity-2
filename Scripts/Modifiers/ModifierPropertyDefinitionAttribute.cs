using System;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// Attribute for modifiers to tell the editor which property type to use (should derive from PropertyBase).
    /// Written as a mitigation for the lack of support for serialization of non-generic nested classes within generic classes.
    ///
    /// For instance, ColorModifier<T> defines a non-generic Property class, but this can't be used for serialization within
    /// the class itself, because it's generic - so we need to reference the ColorProperty class from a custom attribute.
    /// </summary>
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