using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    public static class Utils
    {
        public static List<T> FindAssetsByType<T>() where T : UnityEngine.Object
        {
            List<T> assets = new List<T>();
            string[] guids = AssetDatabase.FindAssets(string.Format("t:{0}", typeof(T)));
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (asset != null)
                {
                    assets.Add(asset as T);
                }
            }
            return assets;
        }

        public static IList<Type> GetSubtypes<T>()
        {
            return Assembly
                .GetAssembly(typeof(T)).GetTypes()
                .Where(myType => myType.IsClass && !myType.IsAbstract 
                    && (myType.IsSubclassOf(typeof(T)) || typeof(T).IsAssignableFrom(myType)))
                .ToList();
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
    }
}
