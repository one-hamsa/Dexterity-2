using System;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// class that is paired with the ObjectValueAttribute to read a value from a Unity Object.
    /// it comes with a dropdown drawer that allows to select the field to read from the object.
    /// 
    /// this wrapper takes care of reflection substitute in form of Expression Trees.
    /// it is AOT safe and can be used in builds. some work required to make it possible - AOT
    /// does not support all types of delegates, so a void delegate is used instead of a generic Func<T> one.
    /// </summary>
    public class ObjectValueContext
    {
        [Flags]
        public enum ValueType
        {
            Boolean = 1 << 0,
            Enum = 1 << 1,
        }
        
        protected delegate void AssignDelegate();
        protected AssignDelegate assign;
        protected readonly UnityEngine.Object unityObject;
        public readonly Type type;
        protected ValueType actualObjectValueType;

        public ObjectValueContext(object callerObject, string attributeFieldName)
        {
            if (callerObject == null)
                throw new ArgumentException($"caller object is null");
            
            var fieldWithAttribute = callerObject.GetType().GetField(attributeFieldName);
            if (fieldWithAttribute == null)
                throw new ArgumentException($"could not find field {attributeFieldName} in {callerObject.GetType().Name}");
            
            var attr = (ObjectValueAttribute)fieldWithAttribute.GetCustomAttribute(typeof(ObjectValueAttribute));
            
            var targetFieldInfo = callerObject.GetType().GetField(attr.objectFieldName);
            if (targetFieldInfo == null)
                throw new ArgumentException($"could not find field {attr.objectFieldName} in {callerObject.GetType().Name}");
            
            unityObject = targetFieldInfo.GetValue(callerObject) as UnityEngine.Object;
            if (unityObject == null)
                throw new ArgumentException($"field {attr.objectFieldName} in {callerObject.GetType().Name} is null or not a Unity Object");
            
            var field = (string)callerObject.GetType().GetField(attributeFieldName).GetValue(callerObject);

            var methodInfo = unityObject.GetType().GetMethod(field, BindingFlags.Public | BindingFlags.Instance);
            if (methodInfo != null)
            {
                type = methodInfo.ReturnType;
                FindActualValueType(attr.supportedTypes);
                assign = CreateDelegateForMethod(methodInfo);
                return;
            }

            var fieldInfo = unityObject.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance);
            if (fieldInfo != null) {
                type = fieldInfo.FieldType;
                FindActualValueType(attr.supportedTypes);
                assign = CreateDelegateForField(fieldInfo);
                return;
            }
            
            var propertyInfo = unityObject.GetType().GetProperty(field, BindingFlags.Public | BindingFlags.Instance);
            if (propertyInfo != null) 
            {
                type = propertyInfo.PropertyType;
                FindActualValueType(attr.supportedTypes);
                assign = CreateDelegateForProperty(propertyInfo);
                return;
            }

            throw new ArgumentException($"could not read reflected property {field} in {unityObject.name}");
        }

        private void FindActualValueType(ValueType valueTypes)
        {
            if (valueTypes.Supports(type))
            {
                actualObjectValueType = type == typeof(bool) ? ValueType.Boolean : ValueType.Enum;
                return;
            }
            
            throw new ArgumentException($"field of type {type.Name} is not supported by this ObjectValueContext");
        }

        protected AssignDelegate CreateDelegateForMethod(MethodInfo methodInfo)
        {
            return actualObjectValueType switch 
            {
                ValueType.Boolean => Boolean_CreateDelegateForMethod(methodInfo),
                ValueType.Enum => Enum_CreateDelegateForMethod(methodInfo),
                _ => throw new ArgumentException($"unsupported value type {actualObjectValueType}"),
            };
        }

        protected AssignDelegate CreateDelegateForField(FieldInfo fieldInfo)
        {
            return actualObjectValueType switch 
            {
                ValueType.Boolean => Boolean_CreateDelegateForField(fieldInfo),
                ValueType.Enum => Enum_CreateDelegateForField(fieldInfo),
                _ => throw new ArgumentException($"unsupported value type {actualObjectValueType}"),
            };
        }

        protected AssignDelegate CreateDelegateForProperty(PropertyInfo propertyInfo)
        {
            return actualObjectValueType switch 
            {
                ValueType.Boolean => Boolean_CreateDelegateForProperty(propertyInfo),
                ValueType.Enum => Enum_CreateDelegateForProperty(propertyInfo),
                _ => throw new ArgumentException($"unsupported value type {actualObjectValueType}"),
            };
        }

        public int GetValueAsInt()
        {
            return actualObjectValueType switch 
            {
                ValueType.Boolean => Boolean_GetValue() ? 1 : 0,
                ValueType.Enum => Enum_GetValue(),
                _ => throw new ArgumentException($"unsupported value type {actualObjectValueType}"),
            };
        }
        
        #region Boolean
        private bool boolean_value;
        private delegate bool Boolean_GetDelegate();
        private Boolean_GetDelegate boolean_get;
        
        public bool Boolean_GetValue()
        {
            assign();
            return boolean_value;
        }
        
        private void Boolean_Assign() => boolean_value = boolean_get();

        protected AssignDelegate Boolean_CreateDelegateForMethod(MethodInfo methodInfo)
        {
            boolean_get = (Boolean_GetDelegate)Delegate.CreateDelegate(typeof(Boolean_GetDelegate), unityObject, methodInfo);
            return Boolean_Assign;
        }

        protected AssignDelegate Boolean_CreateDelegateForField(FieldInfo fieldInfo)
        {
            Debug.LogWarning($"using expressions for field {fieldInfo.Name} in {unityObject.name} (slower and more GC)", unityObject);
            var expr = Expression.Field(Expression.Constant(unityObject), fieldInfo);
            var field = Expression.Field(Expression.Constant(this), nameof(boolean_value));
            var assignExpr = Expression.Assign(field, expr);
            
            return Expression.Lambda<AssignDelegate>(assignExpr).Compile();
        }

        protected AssignDelegate Boolean_CreateDelegateForProperty(PropertyInfo propertyInfo)
        {
            boolean_get = (Boolean_GetDelegate)Delegate.CreateDelegate(typeof(Boolean_GetDelegate), unityObject, propertyInfo.GetGetMethod());
            return Boolean_Assign;
        }
        #endregion
    
        #region Enum
        private int enumValue;
        private delegate int Enum_GetDelegate();
        private Enum_GetDelegate enum_get;
        
        public int Enum_GetValue()
        {
            assign();
            return enumValue;
        }
        
        private void Enum_Assign() => enumValue = enum_get();

        protected AssignDelegate Enum_CreateDelegateForMethod(MethodInfo methodInfo)
        {
            try
            {
                enum_get = (Enum_GetDelegate)Delegate.CreateDelegate(typeof(Enum_GetDelegate), unityObject, methodInfo);
                return Enum_Assign;
            }
            catch (Exception)
            {
                // can happen when the enum is not an int (flags)
                Debug.LogWarning(
                    $"could not create delegate for method {methodInfo.Name} in {unityObject.name}, " +
                    $"falling back to expressions (slower and more GC)", unityObject);
                
                var expr = Expression.Call(Expression.Constant(unityObject), methodInfo);
                var field = Expression.Field(Expression.Constant(this), nameof(enumValue));
                var convertExpr = Expression.Convert(expr, typeof(int));
                var assignExpr = Expression.Assign(field, convertExpr);
                
                return Expression.Lambda<AssignDelegate>(assignExpr).Compile();
            }
        }

        protected AssignDelegate Enum_CreateDelegateForField(FieldInfo fieldInfo)
        {
            Debug.LogWarning($"using expressions for field {fieldInfo.Name} in {unityObject.name} (slower and more GC)", unityObject);
            var expr = Expression.Field(Expression.Constant(unityObject), fieldInfo);
            var field = Expression.Field(Expression.Constant(this), nameof(enumValue));
            var convertExpr = Expression.Convert(expr, typeof(int));
            var assignExpr = Expression.Assign(field, convertExpr);
            
            return Expression.Lambda<AssignDelegate>(assignExpr).Compile();
        }

        protected AssignDelegate Enum_CreateDelegateForProperty(PropertyInfo propertyInfo)
        {
            try
            {
                enum_get = (Enum_GetDelegate)Delegate.CreateDelegate(typeof(Enum_GetDelegate), unityObject, propertyInfo.GetGetMethod());
                return Enum_Assign;
            }
            catch (Exception)
            {
                // can happen when the enum is not an int (flags)
                Debug.LogWarning(
                    $"could not create delegate for property {propertyInfo.Name} in {unityObject.name}, " +
                    $"falling back to expressions (slower and more GC)", unityObject);
                
                var expr = Expression.Property(Expression.Constant(unityObject), propertyInfo);
                var field = Expression.Field(Expression.Constant(this), nameof(enumValue));
                var convertExpr = Expression.Convert(expr, typeof(int));
                var assignExpr = Expression.Assign(field, convertExpr);
                
                return Expression.Lambda<AssignDelegate>(assignExpr).Compile();
            }
        }
        #endregion
    }
    
    public static class ObjectValueContextExtensions
    {
        public static bool Supports(this ObjectValueContext.ValueType valueTypes, Type type)
        {
            if (type == typeof(bool))
                return valueTypes.HasFlag(ObjectValueContext.ValueType.Boolean);

            if (typeof(Enum).IsAssignableFrom(type) && !type.IsDefined(typeof(FlagsAttribute), false))
                return valueTypes.HasFlag(ObjectValueContext.ValueType.Enum);

            return false;
        }
    }
}