using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    public static class Utils
    {
        public static IEnumerable<T> FindAssetsByType<T>() where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets(string.Format("t:{0}", typeof(T)));
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (asset != null)
                    yield return asset as T;
            }
        }

        public static IEnumerable<SerializedProperty> GetChildren(SerializedProperty serializedProperty)
        {
            SerializedProperty currentProperty = serializedProperty.Copy();
            SerializedProperty nextSiblingProperty = serializedProperty.Copy();
            {
                nextSiblingProperty.Next(false);
            }

            if (currentProperty.Next(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(currentProperty, nextSiblingProperty))
                        break;

                    yield return currentProperty;
                }
                while (currentProperty.Next(false));
            }
        }
        public static IEnumerable<SerializedProperty> GetVisibleChildren(SerializedProperty serializedProperty)
        {
            SerializedProperty currentProperty = serializedProperty.Copy();
            SerializedProperty nextSiblingProperty = serializedProperty.Copy();
            /*{
                nextSiblingProperty.NextVisible(false);
            }*/

            if (currentProperty.NextVisible(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(currentProperty, nextSiblingProperty))
                        break;

                    yield return currentProperty;
                }
                while (currentProperty.NextVisible(false));
            }
        }
        public static string GetClassName(SerializedProperty property)
            => property.managedReferenceFullTypename.Contains(' ')
            ? property.managedReferenceFullTypename.Split(' ')[1]
            : null;
        public static IEnumerable<string> GetNiceName(IEnumerable<string> fullNames, string suffix)
        {
            return fullNames.Select(name => {
                var lastPart = name.Split('.').Last();
                if (lastPart.EndsWith(suffix))
                    lastPart = lastPart.Substring(0, lastPart.Length - suffix.Length);
                return lastPart;
            });
        }

        public static string ConvertFieldValueToText(int value, FieldDefinition definition)
        {
            string strValue = value.ToString();
            switch (definition.type)
            {
                case FieldNode.FieldType.Boolean when value != FieldNode.emptyFieldValue:
                    strValue = value == 1 ? "True" : "False";
                    break;

                case FieldNode.FieldType.Enum when value != FieldNode.emptyFieldValue:
                    strValue = definition.enumValues[(int)value];
                    break;
            }

            return strValue;
        }

        public static IEnumerable<string> GetStatesFromObject(UnityEngine.Object unityObject)
        {
            if (unityObject is IHasStates statesProvider)
                return statesProvider.GetStateNames();

            if (unityObject is MonoBehaviour monoBehaviour) {
                foreach (var potentialNode in monoBehaviour.GetComponents<IHasStates>())
                {
                    if (potentialNode is MonoBehaviour { enabled: false })
                        continue;
                    
                    return GetStatesFromObject(potentialNode as UnityEngine.Object);
                }
            }

            return null;
        }


        /// <summary>
        /// Gets the object the property represents.
        /// </summary>
        /// <param name="prop"></param>
        /// <returns></returns>
        /// <see cref="https://github.com/lordofduct/spacepuppy-unity-framework/blob/master/SpacepuppyBaseEditor/EditorHelper.cs"/>
        public static object GetTargetObjectOfProperty(SerializedProperty prop)
        {
            if (prop == null) return null;

            var path = prop.propertyPath.Replace(".Array.data[", "[");
            object obj = prop.serializedObject.targetObject;
            var elements = path.Split('.');
            foreach (var element in elements)
            {
                if (element.Contains("["))
                {
                    var elementName = element.Substring(0, element.IndexOf("["));
                    var index = System.Convert.ToInt32(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
                    obj = GetValue_Imp(obj, elementName, index);
                }
                else
                {
                    obj = GetValue_Imp(obj, element);
                }
            }
            return obj;
        }

        private static object GetValue_Imp(object source, string name)
        {
            if (source == null)
                return null;
            var type = source.GetType();

            while (type != null)
            {
                var f = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (f != null)
                    return f.GetValue(source);

                var p = type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (p != null)
                    return p.GetValue(source, null);

                type = type.BaseType;
            }
            return null;
        }

        private static object GetValue_Imp(object source, string name, int index)
        {
            var enumerable = GetValue_Imp(source, name) as System.Collections.IEnumerable;
            if (enumerable == null) return null;
            var enm = enumerable.GetEnumerator();
            //while (index-- >= 0)
            //    enm.MoveNext();
            //return enm.Current;

            for (int i = 0; i <= index; i++)
            {
                if (!enm.MoveNext()) return null;
            }
            return enm.Current;
        }

    }
}
