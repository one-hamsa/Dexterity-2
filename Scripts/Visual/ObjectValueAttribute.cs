using System;
using UnityEngine;
using System.Reflection;
using UnityEngine.Scripting;

namespace OneHamsa.Dexterity
{
    public class ObjectValueAttribute : PropertyAttribute
    {
		public string objectFieldName;
        public Type fieldType;

        public bool showMethods;
        public bool showProperties;
        public bool showFields;

        public ObjectValueAttribute(string objectFieldName, Type fieldType,
            bool showMethods = true, bool showProperties = true,
            // getting fields by reflection/expressions is gc heavy on il2cpp, hide by default
            bool showFields = false)
        {
            this.objectFieldName = objectFieldName;
            this.fieldType = fieldType;
            this.showMethods = showMethods;
            this.showProperties = showProperties;
            this.showFields = showFields;
        }

    }
}