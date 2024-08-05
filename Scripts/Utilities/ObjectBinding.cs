using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Serialization;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// class that is paired with the ObjectBindingAttribute to read a value from a Unity Object.
    /// it comes with a dropdown drawer that allows to select the field to read from the object.
    /// 
    /// this wrapper takes care of reflection substitute in form of Delegates.
    /// it is AOT safe and can be used in builds. some work required to make it possible - AOT
    /// does not support all types of delegates, so a void delegate is used instead of a generic Func T one.
    /// </summary>
    [Serializable]
    public abstract class ObjectBinding 
    {
        [Flags]
        public enum ValueType
        {
            Boolean = 1 << 0,
            Enum = 1 << 1,
            Int = 2 << 1,
        }
        
        [SerializeField]
        public UnityEngine.Object target;
        [SerializeField]
        public string methodName;
        
        public abstract ValueType supportedTypes { get; }
        
        protected delegate void AssignDelegate();
        protected AssignDelegate assign;
        public Type type { get; private set; }
        protected ValueType actualObjectValueType;
        private StringBuilder sb;

        public override string ToString()
        {
            sb ??= new StringBuilder();
            sb.Clear();
            
            sb.Append(target.name);
            sb.Append('.');
            sb.Append(methodName);
            return sb.ToString();
        }

        public bool IsValid() => target != null && !string.IsNullOrEmpty(methodName);
        public bool IsInitialized() => assign != null;
        public bool Initialize()
        {
            if (!IsValid())
                return false;

            var methodInfo = Reflection.GetMethod(target.GetType(), methodName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            if (methodInfo != null)
            {
                type = methodInfo.ReturnType;
                FindActualValueType(supportedTypes);
                assign = CreateDelegateForMethod(methodInfo);
                return true;
            }

            var fieldInfo = Reflection.GetField(target.GetType(),methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            if (fieldInfo != null) {
                type = fieldInfo.FieldType;
                FindActualValueType(supportedTypes);
                assign = CreateDelegateForField(fieldInfo);
                return true;
            }
            
            var propertyInfo = Reflection.GetProperty(target.GetType(), methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            if (propertyInfo != null) 
            {
                type = propertyInfo.PropertyType;
                FindActualValueType(supportedTypes);
                assign = CreateDelegateForProperty(propertyInfo);
                return true;
            }

            return false;
        }

        private void FindActualValueType(ValueType valueTypes)
        {
            if (valueTypes.Supports(type))
            {
                if (type == typeof(bool))
                    actualObjectValueType = ValueType.Boolean;
                else if (type == typeof(int))
                    actualObjectValueType = ValueType.Int;
                else 
                    actualObjectValueType = ValueType.Enum;
                return;
            }
            
            throw new ArgumentException($"field of type {type.Name} is not supported by this ObjectValueContext");
        }

        protected AssignDelegate CreateDelegateForMethod(MethodInfo methodInfo)
        {
            return actualObjectValueType switch 
            {
                ValueType.Boolean => Boolean_CreateDelegateForMethod(methodInfo),
                ValueType.Enum => Int_CreateDelegateForMethod(methodInfo),
                ValueType.Int => Int_CreateDelegateForMethod(methodInfo),
                _ => throw new ArgumentException($"unsupported value type {actualObjectValueType}"),
            };
        }

        protected AssignDelegate CreateDelegateForField(FieldInfo fieldInfo)
        {
            return actualObjectValueType switch 
            {
                ValueType.Boolean => Boolean_CreateDelegateForField(fieldInfo),
                ValueType.Enum => Int_CreateDelegateForField(fieldInfo),
                ValueType.Int => Int_CreateDelegateForField(fieldInfo),
                _ => throw new ArgumentException($"unsupported value type {actualObjectValueType}"),
            };
        }

        protected AssignDelegate CreateDelegateForProperty(PropertyInfo propertyInfo)
        {
            return actualObjectValueType switch 
            {
                ValueType.Boolean => Boolean_CreateDelegateForProperty(propertyInfo),
                ValueType.Enum => Int_CreateDelegateForProperty(propertyInfo),
                ValueType.Int => Int_CreateDelegateForProperty(propertyInfo),
                _ => throw new ArgumentException($"unsupported value type {actualObjectValueType}"),
            };
        }

        public int GetValueAsInt()
        {
            return actualObjectValueType switch 
            {
                ValueType.Boolean => Boolean_GetValue() ? 1 : 0,
                ValueType.Enum => Int_GetValue(),
                ValueType.Int => Int_GetValue(),
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
            boolean_get = (Boolean_GetDelegate)Delegate.CreateDelegate(typeof(Boolean_GetDelegate), 
                !methodInfo.IsStatic ? target : null, methodInfo);
            return Boolean_Assign;
        }

        protected AssignDelegate Boolean_CreateDelegateForField(FieldInfo fieldInfo)
        {
            Debug.LogWarning($"using expressions for field {fieldInfo.Name} in {target.name} (slower and more GC)", target);
            var expr = Expression.Field(Expression.Constant(!fieldInfo.IsStatic ? target : null), fieldInfo);
            var field = Expression.Field(Expression.Constant(this), nameof(boolean_value));
            var assignExpr = Expression.Assign(field, expr);
            
            return Expression.Lambda<AssignDelegate>(assignExpr).Compile();
        }

        protected AssignDelegate Boolean_CreateDelegateForProperty(PropertyInfo propertyInfo)
        {
            boolean_get = (Boolean_GetDelegate)Delegate.CreateDelegate(typeof(Boolean_GetDelegate), 
                !propertyInfo.GetMethod.IsStatic ? target : null, propertyInfo.GetGetMethod());
            return Boolean_Assign;
        }
        #endregion
    
        #region Int
        private int intValue;
        private delegate int Int_GetDelegate();
        private Int_GetDelegate int_get;
        
        public int Int_GetValue()
        {
            assign();
            return intValue;
        }
        
        private void Int_Assign() => intValue = int_get();

        protected AssignDelegate Int_CreateDelegateForMethod(MethodInfo methodInfo)
        {
            try
            {
                int_get = (Int_GetDelegate)Delegate.CreateDelegate(typeof(Int_GetDelegate), 
                    !methodInfo.IsStatic ? target : null, methodInfo);
                return Int_Assign;
            }
            catch (Exception)
            {
                // can happen when the enum is not an int (flags)
                Debug.LogWarning(
                    $"could not create delegate for method {methodInfo.Name} in {target.name}, " +
                    $"falling back to expressions (slower and more GC)", target);
                
                var expr = Expression.Call(
                    Expression.Constant(!methodInfo.IsStatic ? target : null), methodInfo);
                var field = Expression.Field(Expression.Constant(this), nameof(intValue));
                var convertExpr = Expression.Convert(expr, typeof(int));
                var assignExpr = Expression.Assign(field, convertExpr);
                
                return Expression.Lambda<AssignDelegate>(assignExpr).Compile();
            }
        }

        protected AssignDelegate Int_CreateDelegateForField(FieldInfo fieldInfo)
        {
            Debug.LogWarning($"using expressions for field {fieldInfo.Name} in {target.name} (slower and more GC)", target);
            var expr = Expression.Field(
                Expression.Constant(!fieldInfo.IsStatic ? target : null), fieldInfo);
            var field = Expression.Field(Expression.Constant(this), nameof(intValue));
            var convertExpr = Expression.Convert(expr, typeof(int));
            var assignExpr = Expression.Assign(field, convertExpr);
            
            return Expression.Lambda<AssignDelegate>(assignExpr).Compile();
        }

        protected AssignDelegate Int_CreateDelegateForProperty(PropertyInfo propertyInfo)
        {
            try
            {
                int_get = (Int_GetDelegate)Delegate.CreateDelegate(typeof(Int_GetDelegate), 
                    !propertyInfo.GetMethod.IsStatic ? target : null, propertyInfo.GetGetMethod());
                return Int_Assign;
            }
            catch (Exception)
            {
                // can happen when the enum is not an int (flags)
                Debug.LogWarning(
                    $"could not create delegate for property {propertyInfo.Name} in {target.name}, " +
                    $"falling back to expressions (slower and more GC)", target);
                
                var expr = Expression.Property(
                    Expression.Constant(!propertyInfo.GetMethod.IsStatic ? target : null), propertyInfo);
                var field = Expression.Field(Expression.Constant(this), nameof(intValue));
                var convertExpr = Expression.Convert(expr, typeof(int));
                var assignExpr = Expression.Assign(field, convertExpr);
                
                return Expression.Lambda<AssignDelegate>(assignExpr).Compile();
            }
        }
        #endregion
    }

    public static class ObjectBindingExtensions
    {
        public static bool Supports(this ObjectBinding.ValueType valueTypes, Type type)
        {
            if (type == typeof(bool))
                return valueTypes.HasFlag(ObjectBinding.ValueType.Boolean);
            
            if (type == typeof(int))
                return valueTypes.HasFlag(ObjectBinding.ValueType.Int);

            if (typeof(Enum).IsAssignableFrom(type) && !type.IsDefined(typeof(FlagsAttribute), false))
                return valueTypes.HasFlag(ObjectBinding.ValueType.Enum);

            return false;
        }
    }
}