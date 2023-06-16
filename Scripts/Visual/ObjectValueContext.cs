using System;
using System.Linq.Expressions;
using System.Reflection;

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
    public abstract class ObjectValueContext
    {
        protected delegate void AssignDelegate();
        protected AssignDelegate assign;
        protected readonly UnityEngine.Object unityObject;
        public readonly Type type;

        protected ObjectValueContext(object callerObject, string attributeFieldName) 
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
                assign = CreateDelegateForMethod(methodInfo);
                return;
            }

            var fieldInfo = unityObject.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance);
            if (fieldInfo != null) {
                type = fieldInfo.FieldType;
                assign = CreateDelegateForField(fieldInfo);
                return;
            }
            
            var propertyInfo = unityObject.GetType().GetProperty(field, BindingFlags.Public | BindingFlags.Instance);
            if (propertyInfo != null) {
                type = propertyInfo.PropertyType;
                assign = CreateDelegateForProperty(propertyInfo);
                return;
            }

            throw new ArgumentException($"could not read reflected property {field} in {unityObject.name}");
        }

        protected abstract AssignDelegate CreateDelegateForMethod(MethodInfo methodInfo);
        protected abstract AssignDelegate CreateDelegateForField(FieldInfo fieldInfo);
        protected abstract AssignDelegate CreateDelegateForProperty(PropertyInfo propertyInfo);
    }

    public class ObjectBooleanContext : ObjectValueContext
    {
        private bool booleanValue;
        private delegate bool GetDelegate();
        private GetDelegate get;
        
        public ObjectBooleanContext(object callerObject, string attributeFieldName) 
            : base(callerObject, attributeFieldName)
        {
        }
        
        public bool GetValue()
        {
            assign();
            return booleanValue;
        }
        
        private void Assign() => booleanValue = get();

        protected override AssignDelegate CreateDelegateForMethod(MethodInfo methodInfo)
        {
            get = (GetDelegate)Delegate.CreateDelegate(typeof(GetDelegate), unityObject, methodInfo);
            return Assign;
        }

        protected override AssignDelegate CreateDelegateForField(FieldInfo fieldInfo)
        {
            var expr = Expression.Field(Expression.Constant(unityObject), fieldInfo);
            var field = Expression.Field(Expression.Constant(this), nameof(booleanValue));
            var assignExpr = Expression.Assign(field, expr);
            
            return Expression.Lambda<AssignDelegate>(assignExpr).Compile();
        }

        protected override AssignDelegate CreateDelegateForProperty(PropertyInfo propertyInfo)
        {
            get = (GetDelegate)Delegate.CreateDelegate(typeof(GetDelegate), unityObject, propertyInfo.GetGetMethod());
            return Assign;
        }
    }
    
    public class ObjectEnumContext : ObjectValueContext
    {
        private int enumValue;
        private delegate int GetDelegate();
        private GetDelegate get;
        
        public ObjectEnumContext(object callerObject, string attributeFieldName) 
            : base(callerObject, attributeFieldName)
        {
        }
        
        public int GetValue()
        {
            assign();
            return enumValue;
        }
        
        private void Assign() => enumValue = get();

        protected override AssignDelegate CreateDelegateForMethod(MethodInfo methodInfo)
        {
            try
            {
                get = (GetDelegate)Delegate.CreateDelegate(typeof(GetDelegate), unityObject, methodInfo);
                return Assign;
            }
            catch (Exception)
            {
                // can happen when the enum is not an int (flags)
                var expr = Expression.Call(Expression.Constant(unityObject), methodInfo);
                var field = Expression.Field(Expression.Constant(this), nameof(enumValue));
                var convertExpr = Expression.Convert(expr, typeof(int));
                var assignExpr = Expression.Assign(field, convertExpr);
                
                return Expression.Lambda<AssignDelegate>(assignExpr).Compile();
            }
        }

        protected override AssignDelegate CreateDelegateForField(FieldInfo fieldInfo)
        {
            var expr = Expression.Field(Expression.Constant(unityObject), fieldInfo);
            var field = Expression.Field(Expression.Constant(this), nameof(enumValue));
            var convertExpr = Expression.Convert(expr, typeof(int));
            var assignExpr = Expression.Assign(field, convertExpr);
            
            return Expression.Lambda<AssignDelegate>(assignExpr).Compile();
        }

        protected override AssignDelegate CreateDelegateForProperty(PropertyInfo propertyInfo)
        {
            try
            {
                get = (GetDelegate)Delegate.CreateDelegate(typeof(GetDelegate), unityObject, propertyInfo.GetGetMethod());
                return Assign;
            }
            catch (Exception)
            {
                // can happen when the enum is not an int (flags)
                var expr = Expression.Property(Expression.Constant(unityObject), propertyInfo);
                var field = Expression.Field(Expression.Constant(this), nameof(enumValue));
                var convertExpr = Expression.Convert(expr, typeof(int));
                var assignExpr = Expression.Assign(field, convertExpr);
                
                return Expression.Lambda<AssignDelegate>(assignExpr).Compile();
            }
        }
    }
}