using System;
using System.Collections.Generic;
using System.Reflection;

namespace OneHamsa.Dexterity
{
    internal static class Reflection
    {
        private static readonly Dictionary<(Type, BindingFlags), List<FieldInfo>> _fieldCache = new();
        private static readonly Dictionary<(Type, BindingFlags), List<PropertyInfo>> _propertyCache = new();
        private static readonly Dictionary<(Type, BindingFlags), List<MethodInfo>> _methodCache = new();
        
        public static List<FieldInfo> GetFields(Type type, BindingFlags flags)
        {
            if (!_fieldCache.TryGetValue((type, flags), out var fields))
            {
                fields = new List<FieldInfo>(type.GetFields(flags));
                _fieldCache.Add((type, flags), fields);
            }
            return fields;
        }
        
        public static List<PropertyInfo> GetProperties(Type type, BindingFlags flags)
        {
            if (!_propertyCache.TryGetValue((type, flags), out var properties))
            {
                properties = new List<PropertyInfo>(type.GetProperties(flags));
                _propertyCache.Add((type, flags), properties);
            }
            return properties;
        }
        
        public static List<MethodInfo> GetMethods(Type type, BindingFlags flags)
        {
            if (!_methodCache.TryGetValue((type, flags), out var methods))
            {
                methods = new List<MethodInfo>(type.GetMethods(flags));
                _methodCache.Add((type, flags), methods);
            }
            return methods;
        }
        
        public static MethodInfo GetMethod(Type type, string name, BindingFlags flags)
        {
            var methods = GetMethods(type, flags);
            foreach (var method in methods)
            {
                if (method.Name == name)
                    return method;
            }
            return null;
        }
        
        public static FieldInfo GetField(Type type, string name) => GetField(type, name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public);

        public static FieldInfo GetField(Type type, string name, BindingFlags flags)
        {
            var fields = GetFields(type, flags);
            foreach (var field in fields)
            {
                if (field.Name == name)
                    return field;
            }
            return null;
        }
        
        public static PropertyInfo GetProperty(Type type, string name, BindingFlags flags)
        {
            var properties = GetProperties(type, flags);
            foreach (var property in properties)
            {
                if (property.Name == name)
                    return property;
            }
            return null;
        }
    }
}