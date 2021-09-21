using System;
using UnityEngine;
using System.Reflection;

namespace OneHamsa.Dexterity.Visual
{
    public class ObjectValueAttribute : PropertyAttribute
    {
		public string objectFieldName;
        public Type fieldType;

        public ObjectValueAttribute(string objectFieldName, Type fieldType)
        {
            this.objectFieldName = objectFieldName;
            this.fieldType = fieldType;
        }

        public static T Read<T>(UnityEngine.Object unityObject, string propertyName)
        {
            var method = unityObject.GetType().GetMethod(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (method != null)
                return (T)method.Invoke(unityObject, null);

            var field = unityObject.GetType().GetField(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
                return (T)field.GetValue(unityObject);

            var prop = unityObject.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null)
                return (T)prop.GetValue(unityObject);

            Debug.LogError($"could not read reflected property {propertyName} in {unityObject.name}");
            return default;
        }
    }
}