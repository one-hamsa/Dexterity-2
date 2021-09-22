using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using UnityEditorInternal;
using System;

namespace OneHamsa.Dexterity.Visual
{
    public static class DexteritySettingsProvider
    {
        static UnityEngine.Object FindAssetByType(Type t)
        {
            List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
            string[] guids = AssetDatabase.FindAssets(string.Format("t:{0}", t));

            if (guids.Length == 0)
            {
                Debug.LogError("no DexteritySettings found in project");
                return null;
            }

            if (guids.Length > 1)
            {
                Debug.LogError("more than one DexteritySettings found in project");
                for (int i = 0; i < guids.Length; i++)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                    Debug.LogError($"{i + 1}: {assetPath}");
                }
                return null;
            }

            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                return asset;
            }
        }

        private static DexteritySettings cachedSettings;
        public static DexteritySettings settings {
            get
            {
                if (cachedSettings == null)
                {
                    var s = (DexteritySettings)FindAssetByType(typeof(DexteritySettings));
                    if (s == null)
                        return s;

                    cachedSettings = s;
                }
                return cachedSettings;
            }
        }


        /**
         * returns field definition by name - slow.
         */
        public static FieldDefinition GetFieldDefinitionByName(string name)
        {
            foreach (var fd in settings.fieldDefinitions)
                if (fd.name == name)
                    return fd;

            return default;
        }
    }
}