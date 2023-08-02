using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using UnityEditorInternal;
using System;

namespace OneHamsa.Dexterity
{
    public static class DexteritySettingsProvider
    {
        static UnityEngine.Object FindAssetByType(Type t)
        {
            var guids = AssetDatabase.FindAssets($"t:{t}");

            if (guids.Length == 0)
                return null;

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

        public static DexteritySettings TryGetSettings()
        {
            if (cachedSettings == null)
            {
                var s = (DexteritySettings)FindAssetByType(typeof(DexteritySettings));
                if (s == null)
                {
                    return s;
                }
                cachedSettings = s;
            }

            return cachedSettings;
        }
        public static DexteritySettings settings {
            get
            {
                var s = TryGetSettings();
                if (s == null)
                    Debug.LogError("no DexteritySettings found in project");
                
                return s;
            }
        }


        /// <summary>
        /// returns field definition by name - slow.
        /// </summary>
        public static FieldDefinition GetFieldDefinitionByName(IGateContainer gateContainer, string name)
        {
            // we can't use Database here since it's not initialized in edit mode, so we have to do it manually
            
            var internalField = gateContainer?.GetInternalFieldDefinitions()
                .FirstOrDefault(fd => fd.GetName() == name);

            if (!string.IsNullOrEmpty(internalField?.GetName()))
                return internalField.Value;

            foreach (var fd in settings.fieldDefinitions)
                if (fd.GetName() == name)
                    return fd;

            return default;
        }
    }
}