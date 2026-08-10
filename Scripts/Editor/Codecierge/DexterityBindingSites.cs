using System.Collections.Generic;
using System.Text;
using OneHumus.AssetReferenceIndex;
using UnityEditor;
using UnityEngine;

// Namespace matches the rest of Dexterity's editor code deliberately: a nested `.Editor`
// namespace would shadow UnityEditor.Editor for every file in this assembly.
namespace OneHamsa.Dexterity
{
    /// <summary>
    /// One place where a Dexterity binding drives a member: the component holding the binding,
    /// the GameObject it sits on, and the asset containing both.
    /// </summary>
    internal struct DexterityBindingSite
    {
        public string AssetPath;
        public long OwnerFileId;
        public string OwnerGoName;   // from the index; the only name available when nothing is loaded
        public string Member;        // resolved member, e.g. IsPhotoBoothActive
        public string StoredName;    // what Dexterity reflects on, e.g. get_IsPhotoBoothActive
        public int LineNumber;       // member's declaration line, for the source-view badge
    }

    /// <summary>
    /// Shared lookup + display helpers for Dexterity binding sites, used by both the Reference
    /// window section and the View Source line annotator.
    ///
    /// The index stores no transform hierarchy — a fileID is only meaningful relative to its
    /// containing asset — so a full path exists only while the scene or prefab is loaded.
    /// <see cref="ResolveComponent"/> reconstitutes the live object from (guid, fileID) via
    /// <see cref="GlobalObjectId"/>, and callers degrade to <see cref="DexterityBindingSite.OwnerGoName"/>
    /// when it can't.
    /// </summary>
    internal static class DexterityBindingSites
    {
        /// <summary>Binding sites for members declared in a .cs file, ordered by line.</summary>
        public static List<DexterityBindingSite> ForScript(string scriptPath)
        {
            return Read(CodeciergeQuery.Select(
                @"SELECT member, member_name, asset_path, owner_fileid, owner_go_name, line_number
                    FROM v_dexterity_bindings WHERE file_path = ? ORDER BY line_number, asset_path", scriptPath));
        }

        /// <summary>Binding sites inside one asset.</summary>
        public static List<DexterityBindingSite> ForAsset(string assetPath)
        {
            return Read(CodeciergeQuery.Select(
                @"SELECT member, member_name, asset_path, owner_fileid, owner_go_name, line_number
                    FROM v_dexterity_bindings WHERE asset_path = ? ORDER BY member", assetPath));
        }

        static List<DexterityBindingSite> Read(List<string[]> rows)
        {
            var results = new List<DexterityBindingSite>(rows.Count);
            foreach (var r in rows)
            {
                long.TryParse(r[3], out var ownerFileId);
                int.TryParse(r[5], out var line);
                results.Add(new DexterityBindingSite
                {
                    Member = r[0],
                    StoredName = r[1],
                    AssetPath = r[2],
                    OwnerFileId = ownerFileId,
                    OwnerGoName = r[4],
                    LineNumber = line,
                });
            }
            return results;
        }

        /// <summary>Groups sites by resolved member, preserving first-seen order.</summary>
        public static List<KeyValuePair<string, List<DexterityBindingSite>>> GroupByMember(List<DexterityBindingSite> sites)
        {
            var order = new List<KeyValuePair<string, List<DexterityBindingSite>>>();
            var index = new Dictionary<string, List<DexterityBindingSite>>();
            foreach (var s in sites)
            {
                var key = s.Member ?? "?";
                if (!index.TryGetValue(key, out var list))
                {
                    list = new List<DexterityBindingSite>();
                    index[key] = list;
                    order.Add(new KeyValuePair<string, List<DexterityBindingSite>>(key, list));
                }
                list.Add(s);
            }
            return order;
        }

        /// <summary>
        /// The live Component, or null when its scene/prefab isn't loaded. The identifier type
        /// isn't knowable from the path alone (2 for a scene object, 1 for one inside an
        /// imported asset), so both are tried and whichever resolves wins.
        /// </summary>
        public static Component ResolveComponent(string assetPath, long fileId)
        {
            if (string.IsNullOrEmpty(assetPath) || fileId == 0) return null;
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid)) return null;

            foreach (var identifierType in new[] { 2, 1 })
            {
                var raw = $"GlobalObjectId_V1-{identifierType}-{guid}-{(ulong)fileId}-0";
                if (!GlobalObjectId.TryParse(raw, out var id)) continue;
                if (GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) is Component c) return c;
            }
            return null;
        }

        /// <summary>
        /// Best available location text: the real hierarchy path when loaded, otherwise
        /// "&lt;Asset&gt;/…/&lt;GameObject&gt;" from the index.
        /// </summary>
        public static string DescribeLocation(in DexterityBindingSite site, Component resolved)
        {
            if (resolved != null) return HierarchyPath(resolved);
            var asset = System.IO.Path.GetFileNameWithoutExtension(site.AssetPath);
            return $"{asset}/…/{site.OwnerGoName ?? "?"}";
        }

        /// <summary>Scene/prefab-relative path, e.g. "Joint/Photo Booth/Width Blockers".</summary>
        public static string HierarchyPath(Component component)
        {
            var names = new List<string>();
            for (var t = component.transform; t != null; t = t.parent) names.Add(t.name);
            names.Reverse();

            var sb = new StringBuilder();
            var scene = component.gameObject.scene;
            if (scene.IsValid() && !string.IsNullOrEmpty(scene.name)) sb.Append(scene.name).Append('/');
            for (int i = 0; i < names.Count; i++)
            {
                if (i > 0) sb.Append('/');
                sb.Append(names[i]);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Pings the component when it's loaded, otherwise the asset containing it — so a click
        /// always lands somewhere useful whether or not the scene is open.
        /// </summary>
        public static void Ping(Component component, string assetPath)
        {
            if (component != null)
            {
                Selection.activeObject = component.gameObject;
                EditorGUIUtility.PingObject(component.gameObject);
                return;
            }
            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null) return;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}
