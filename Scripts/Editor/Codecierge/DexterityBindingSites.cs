using System;
using System.Collections.Generic;
using System.Text;
using OneHumus.AssetReferenceIndex;
using UnityEditor;
using UnityEditor.SceneManagement;
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
        public string FilePath;      // the .cs file declaring the member
        public int LineNumber;       // member's declaration line, for the source-view badge
        public string LineText;      // that line's text, which is where the column comes from
        public string DexterityField;// field the binding drives, when it sits in a BindingField
        public string TrueState;     // BindingEnumNode's boolean states, when it is one
        public string FalseState;
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
                $"SELECT {Columns} FROM v_dexterity_bindings WHERE file_path = ? ORDER BY line_number, asset_path",
                scriptPath));
        }

        /// <summary>Binding sites inside one asset.</summary>
        public static List<DexterityBindingSite> ForAsset(string assetPath)
        {
            return Read(CodeciergeQuery.Select(
                $"SELECT {Columns} FROM v_dexterity_bindings WHERE asset_path = ? ORDER BY member",
                assetPath));
        }

        const string Columns =
            "member, member_name, asset_path, owner_fileid, owner_go_name, line_number, " +
            "file_path, line_text, dexterity_field, true_state, false_state";

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
                    FilePath = r[6],
                    LineText = r[7],
                    DexterityField = r[8],
                    TrueState = r[9],
                    FalseState = r[10],
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
        /// What the binding does, in Dexterity's own terms: the field it drives, or the states
        /// it switches between on a node that has no field. Null when the mapping carried
        /// neither, which is how a GraphNode source with only a target reads.
        /// </summary>
        public static string DescribeEffect(in DexterityBindingSite site)
        {
            if (!string.IsNullOrEmpty(site.DexterityField))
                return $"drives field {site.DexterityField}";
            if (!string.IsNullOrEmpty(site.TrueState) || !string.IsNullOrEmpty(site.FalseState))
                return $"selects {site.TrueState ?? "?"}/{site.FalseState ?? "?"}";
            return null;
        }

        /// <summary>
        /// "Assets/Foo.cs:120:17". The index stores no column, so it comes from the member's
        /// offset within its own declaration line.
        /// </summary>
        public static string DescribeSource(in DexterityBindingSite site)
        {
            if (string.IsNullOrEmpty(site.FilePath)) return null;
            if (site.LineNumber <= 0) return site.FilePath;

            var column = 1;
            if (!string.IsNullOrEmpty(site.LineText) && !string.IsNullOrEmpty(site.Member))
            {
                var offset = site.LineText.IndexOf(site.Member, StringComparison.Ordinal);
                if (offset >= 0) column = offset + 1;
            }
            return $"{site.FilePath}:{site.LineNumber}:{column}";
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
                if (GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) is Component c)
                    return InOpenPrefabStage(c, assetPath);
            }
            return null;
        }

        /// <summary>
        /// The same component inside an open Prefab Mode stage, when the site's prefab is the
        /// one being edited. Selecting the asset-side object while the prefab is open pings the
        /// asset in the Project window instead of the object the user is looking at.
        ///
        /// The stage holds its own copy of the contents rather than an instance of them, so
        /// there is no id or <c>GetCorrespondingObjectFromSource</c> link back to the asset
        /// (both come back null). The transform path is identical on the two sides, so that is
        /// what maps across.
        /// </summary>
        static Component InOpenPrefabStage(Component assetComponent, string assetPath)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null || stage.assetPath != assetPath) return assetComponent;

            var wanted = TransformPath(assetComponent.transform);
            foreach (var t in stage.prefabContentsRoot.GetComponentsInChildren<Transform>(true))
            {
                if (TransformPath(t) != wanted) continue;
                var match = t.GetComponent(assetComponent.GetType());
                if (match != null) return match;
            }
            return assetComponent;
        }

        /// <summary>Root-to-leaf transform path, with no scene prefix - the form both sides share.</summary>
        static string TransformPath(Transform transform)
        {
            var names = new List<string>();
            for (var t = transform; t != null; t = t.parent) names.Add(t.name);
            names.Reverse();
            return string.Join("/", names);
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
            // A prefab stage's scene is named after the prefab, whose root is already the first
            // path element - prefixing it would repeat that name.
            if (scene.IsValid() && !string.IsNullOrEmpty(scene.name)
                && PrefabStageUtility.GetPrefabStage(component.gameObject) == null)
                sb.Append(scene.name).Append('/');
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
