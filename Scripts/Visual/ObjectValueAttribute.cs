using System;
using System.Linq.Expressions;
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

        public static Context CreateContext(object callerObject, string propertyFieldName)
        {
            var field = callerObject.GetType().GetField(propertyFieldName);
            var attr = (ObjectValueAttribute)field.GetCustomAttribute(typeof(ObjectValueAttribute));

            return new Context(callerObject, attr.objectFieldName, propertyFieldName);
        }

        public class Context
        {            
            private readonly UnityEngine.Object unityObject;
            private readonly MemberInfo memberInfo;
            private Func<bool> compiledBoolExpression;
            private Func<Enum> compiledEnumExpression;
            public readonly Type type;

            public Context(object callerObject, string objectFieldName, string propertyFieldName) 
            {
                var fieldInfo = callerObject.GetType().GetField(objectFieldName);
                unityObject = (UnityEngine.Object)fieldInfo.GetValue(callerObject);
                var field = (string)callerObject.GetType().GetField(propertyFieldName).GetValue(callerObject);

                if (memberInfo == null) {
                    memberInfo = unityObject.GetType().GetMethod(field, BindingFlags.Public | BindingFlags.Instance);
                    type = (memberInfo as MethodInfo)?.ReturnType;
                }

                if (memberInfo == null) {
                    memberInfo = unityObject.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance);
                    type = (memberInfo as FieldInfo)?.FieldType;
                }

                if (memberInfo == null) {
                    memberInfo = unityObject.GetType().GetProperty(field, BindingFlags.Public | BindingFlags.Instance);
                    type = (memberInfo as PropertyInfo)?.PropertyType;
                }

                if (memberInfo == null)
                {
                    throw new ArgumentException($"could not read reflected property {field} in {unityObject.name}");
                }
            }

            public bool GetBooleanValue()
            {
                if (compiledBoolExpression == null)
                {
                    // use expressions to avoid allocations
                    var methodInfo = memberInfo as MethodInfo;
                    if (methodInfo != null)
                        compiledBoolExpression = Expression.Lambda<Func<bool>>(Expression.Call(Expression.Constant(unityObject), methodInfo)).Compile();

                    var fieldInfo = memberInfo as FieldInfo;
                    if (fieldInfo != null)
                        compiledBoolExpression = Expression.Lambda<Func<bool>>(Expression.Field(Expression.Constant(unityObject), fieldInfo)).Compile();

                    var propertyInfo = memberInfo as PropertyInfo;
                    if (propertyInfo != null)
                        compiledBoolExpression = Expression.Lambda<Func<bool>>(Expression.Property(Expression.Constant(unityObject), propertyInfo)).Compile();
                }
                
                return compiledBoolExpression?.Invoke() ?? default;
            }
            
            public Enum GetEnumValue()
            {
                if (compiledEnumExpression == null)
                {
                    // use expressions to avoid allocations
                    var methodInfo = memberInfo as MethodInfo;
                    if (methodInfo != null)
                    {
                        // we need to first cast the result to generic enum
                        var castToEnum = Expression.Convert(Expression.Call(Expression.Constant(unityObject), methodInfo), typeof(Enum));
                        compiledEnumExpression = Expression.Lambda<Func<Enum>>(castToEnum).Compile();
                    }
                    
                    var fieldInfo = memberInfo as FieldInfo;
                    if (fieldInfo != null)
                    {
                        // we need to first cast the result to generic enum
                        var castToEnum = Expression.Convert(Expression.Field(Expression.Constant(unityObject), fieldInfo), typeof(Enum));
                        compiledEnumExpression = Expression.Lambda<Func<Enum>>(castToEnum).Compile();
                    }
                    
                    var propertyInfo = memberInfo as PropertyInfo;
                    if (propertyInfo != null)
                    {
                        // we need to first cast the result to generic enum
                        var castToEnum = Expression.Convert(Expression.Property(Expression.Constant(unityObject), propertyInfo), typeof(Enum));
                        compiledEnumExpression = Expression.Lambda<Func<Enum>>(castToEnum).Compile();
                    }
                }

                return compiledEnumExpression?.Invoke();
            }
        }
    }
}