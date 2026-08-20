using System.Collections.Generic;
using OneHumus.AssetReferenceIndex;

// Namespace matches the rest of Dexterity's editor code deliberately: a nested `.Editor`
// namespace would shadow UnityEditor.Editor for every file in this assembly.
namespace OneHamsa.Dexterity
{
    /// <summary>
    /// Badges above a member's declaration in Codecierge's View Source, naming what each
    /// Dexterity binding drives and the GameObject it sits on — one badge per binding
    /// site, since a member is regularly driven from several.
    ///
    /// Dexterity contributes these in C# rather than through the generic member-binding
    /// contract because that contract only knows the containing asset, and "Used in Joint" is
    /// the container, not the user. Clicking selects the component when its scene or prefab is
    /// loaded, and falls back to the containing asset when it isn't.
    /// </summary>
    public class DexterityLineAnnotator : ICodeciergeLineAnnotator
    {
        // Full paths get long; past this the middle is elided so the badge stays readable
        // beside code. The tooltip always carries the whole thing.
        const int MaxPathChars = 44;

        public IEnumerable<CodeciergeLineBadge> Annotate(string scriptPath)
        {
            if (!CodeciergeQuery.IsAvailable) yield break;

            foreach (var site in DexterityBindingSites.ForScript(scriptPath))
            {
                if (site.LineNumber <= 0) continue;

                var component = DexterityBindingSites.ResolveComponent(site.AssetPath, site.OwnerFileId);
                var path = DexterityBindingSites.DescribeLocation(site, component);
                var captured = site;

                var tooltip = component != null
                    ? $"Dexterity binding\n{path}\n{site.AssetPath}"
                    : $"Dexterity binding\n{path}\n{site.AssetPath}\nOpen it to select the component directly.";

                // What the binding drives, when the mapping recorded it: "Used in X" alone
                // says the member is bound somewhere without saying to what.
                var effect = DexterityBindingSites.DescribeEffect(site);
                var text = effect != null ? $"{effect} on {Elide(path)}" : $"Used in {Elide(path)}";

                yield return new CodeciergeLineBadge(
                    site.LineNumber,
                    text,
                    tooltip,
                    "d_GameObject Icon",
                    () => DexterityBindingSites.Ping(
                        DexterityBindingSites.ResolveComponent(captured.AssetPath, captured.OwnerFileId),
                        captured.AssetPath));
            }
        }

        // Keeps the two ends — the container tells you where, the leaf tells you what.
        static string Elide(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Length <= MaxPathChars) return path;

            int firstSlash = path.IndexOf('/');
            int lastSlash = path.LastIndexOf('/');
            if (firstSlash < 0 || lastSlash <= firstSlash) return path;

            return $"{path.Substring(0, firstSlash)}/…/{path.Substring(lastSlash + 1)}";
        }
    }
}
