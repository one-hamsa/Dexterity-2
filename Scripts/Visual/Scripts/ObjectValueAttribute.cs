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

        private UnityEngine.Object unityObject;
        private MemberInfo memberInfo;
        public static ObjectValueAttribute Initialize(object callerObject, string fieldName)
        {
            var field = callerObject.GetType().GetField(fieldName);
            var attr = (ObjectValueAttribute)field.GetCustomAttribute(typeof(ObjectValueAttribute));

            attr.InitializeInternal(callerObject, fieldName);

            return attr;
        }

        private void InitializeInternal(object callerObject, string fieldName)
        {
            // cache
            unityObject = (UnityEngine.Object)callerObject.GetType().GetField(objectFieldName).GetValue(callerObject);
            var field = (string)callerObject.GetType().GetField(fieldName).GetValue(callerObject);

            if (memberInfo == null)
                memberInfo = unityObject.GetType().GetMethod(field, BindingFlags.Public | BindingFlags.Instance);

            if (memberInfo == null)
                memberInfo = unityObject.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance);

            if (memberInfo == null)
                memberInfo = unityObject.GetType().GetProperty(field, BindingFlags.Public | BindingFlags.Instance);

            if (memberInfo == null)
            {
                Debug.LogError($"could not read reflected property {field} in {unityObject.name}");
            }
        }

        public T GetValue<T>()
        {
            var method = memberInfo as MethodInfo;
            if (method != null)
                return (T)method.Invoke(unityObject, null);

            var field = memberInfo as FieldInfo;
            if (field != null)
                return (T)field.GetValue(unityObject);

            var prop = memberInfo as PropertyInfo;
            if (prop != null)
                return (T)prop.GetValue(unityObject);

            return default;
        }
    }
}