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

        public static Context<T> CreateContext<T>(object callerObject, string propertyFieldName)
        {
            var field = callerObject.GetType().GetField(propertyFieldName);
            var attr = (ObjectValueAttribute)field.GetCustomAttribute(typeof(ObjectValueAttribute));

            return new Context<T>(callerObject, attr.objectFieldName, propertyFieldName);
        }

        public class Context<T>
        {            
            private readonly UnityEngine.Object unityObject;
            private readonly MemberInfo memberInfo;
            private Func<T> compiledExpression;
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

            public T GetValue()
            {
                if (compiledExpression == null)
                {
                    // use expressions to avoid allocations
                    var methodInfo = memberInfo as MethodInfo;
                    if (methodInfo != null)
                        compiledExpression = Expression.Lambda<Func<T>>(Expression.Call(Expression.Constant(unityObject), methodInfo)).Compile();

                    var fieldInfo = memberInfo as FieldInfo;
                    if (fieldInfo != null)
                        compiledExpression = Expression.Lambda<Func<T>>(Expression.Field(Expression.Constant(unityObject), fieldInfo)).Compile();

                    var propertyInfo = memberInfo as PropertyInfo;
                    if (propertyInfo != null)
                        compiledExpression = Expression.Lambda<Func<T>>(Expression.Property(Expression.Constant(unityObject), propertyInfo)).Compile();
                }
                
                return compiledExpression != null ? compiledExpression() : default;
            }
        }
    }
}