using System.Collections.Generic;
using OneHumus.AssetReferenceIndex;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// Namespace matches the rest of Dexterity's editor code deliberately: a nested `.Editor`
// namespace would shadow UnityEditor.Editor for every file in this assembly.
namespace OneHamsa.Dexterity
{
    /// <summary>
    /// Surfaces Dexterity's reflection bindings in the Codecierge Reference window.
    ///
    /// Lives here rather than in Codecierge because what a binding MEANS is Dexterity's
    /// business: Codecierge only knows that some asset names some member. This section answers
    /// the question a designer actually has — *which GameObject* drives this member — which the
    /// generic member-binding row can't, since it only knows the containing asset.
    ///
    /// One member is regularly bound from several GameObjects, so each is listed separately with
    /// the component itself as a live object reference when its scene or prefab is open.
    /// </summary>
    public class DexterityBindingSection : ICodeciergeSection
    {
        // After Codecierge's own sections.
        public int Order => 100;

        public VisualElement Build(CodeciergeTarget target)
        {
            if (!CodeciergeQuery.IsAvailable) return null;

            var sites = target.IsScript
                ? DexterityBindingSites.ForScript(target.Path)
                : DexterityBindingSites.ForAsset(target.Path);

            // Bindings whose target is unset in the asset itself — it arrives via a prefab
            // override, so the two halves live in different files. Asset-side only.
            var unresolved = target.IsScript
                ? new List<string[]>()
                : CodeciergeQuery.Select(
                    "SELECT member_name FROM v_dexterity_bindings_unresolved WHERE asset_path = ?", target.Path);

            if (sites.Count == 0 && unresolved.Count == 0) return null;

            var byMember = DexterityBindingSites.GroupByMember(sites);

            var root = new Foldout { text = $"Dexterity Bindings ({byMember.Count + unresolved.Count})", value = true };
            root.AddToClassList("section-foldout");

            foreach (var kvp in byMember)
                root.Add(MakeMemberEntry(kvp.Key, kvp.Value));

            foreach (var u in unresolved)
            {
                var label = new Label($"{u[0]} — target set by a prefab override, not resolvable from this asset");
                label.AddToClassList("empty-message");
                root.Add(label);
            }
            return root;
        }

        static VisualElement MakeMemberEntry(string member, List<DexterityBindingSite> sites)
        {
            var entry = new VisualElement();
            entry.AddToClassList("reference-row");

            var header = new VisualElement();
            header.AddToClassList("ref-top-line");

            var name = new Label(member);
            name.AddToClassList("type-name");
            header.Add(name);

            // The stored name is the accessor Dexterity actually reflects on; showing it makes an
            // otherwise puzzling get_/set_ mismatch obvious at a glance.
            var stored = sites[0].StoredName;
            if (!string.IsNullOrEmpty(stored) && stored != member)
            {
                var via = new Label($"reflects {stored}");
                via.AddToClassList("ref-kind");
                header.Add(via);
            }

            if (sites.Count > 1)
            {
                var count = new Label($"{sites.Count} GameObjects");
                count.AddToClassList("ref-kind");
                header.Add(count);
            }
            entry.Add(header);

            foreach (var site in sites) entry.Add(MakeSiteRow(site));
            return entry;
        }

        /// <summary>
        /// One binding site: its hierarchy path, plus the component itself as an object field
        /// when the containing scene or prefab is loaded.
        /// </summary>
        static VisualElement MakeSiteRow(DexterityBindingSite site)
        {
            var component = DexterityBindingSites.ResolveComponent(site.AssetPath, site.OwnerFileId);

            var row = new VisualElement();
            row.style.marginLeft = 12;
            row.style.marginBottom = 2;

            var pathLabel = new Label(DexterityBindingSites.DescribeLocation(site, component));
            pathLabel.tooltip = component != null
                ? site.AssetPath
                : $"{site.AssetPath}\nOpen it to select the component directly.";
            pathLabel.style.unityFontStyleAndWeight = component != null ? FontStyle.Normal : FontStyle.Italic;
            var capturedPath = site.AssetPath;
            var capturedFileId = site.OwnerFileId;
            pathLabel.RegisterCallback<ClickEvent>(_ => DexterityBindingSites.Ping(
                DexterityBindingSites.ResolveComponent(capturedPath, capturedFileId), capturedPath));
            row.Add(pathLabel);

            // A real object reference when it's loaded — draggable, and it survives a rename that
            // would stale the cached name. Falls back to pinging the containing asset.
            if (component != null)
            {
                var field = new ObjectField { value = component, objectType = typeof(Component) };
                field.SetEnabled(false);   // a view of the binding site, not an edit surface
                row.Add(field);
            }
            else
            {
                var ping = new Button(() => DexterityBindingSites.Ping(null, capturedPath))
                { text = $"Ping {System.IO.Path.GetFileNameWithoutExtension(site.AssetPath)}" };
                ping.AddToClassList("line-link");
                row.Add(ping);
            }
            return row;
        }
    }
}
