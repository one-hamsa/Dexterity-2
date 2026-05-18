using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// Marks a transform subtree as a "preview group" for the Dexterity graph window.
    /// Optional — only needed when you want several related <see cref="GraphNode"/>s
    /// to animate together when any one of them is previewed.
    ///
    /// Lookup: from any node, walk up the transform hierarchy to find the *topmost*
    /// <see cref="GraphNodePreviewRoot"/>; the preview set is then this root's
    /// <see cref="GetAllChildren"/>. Nested roots are supported — the outermost wins
    /// (so a "Page" root containing several "Section" roots previews the whole page).
    ///
    /// Without this component, previewing a node only animates that node by itself.
    /// </summary>
    [AddComponentMenu("Dexterity/Graph Node Preview Root")]
    public class GraphNodePreviewRoot : MonoBehaviour
    {
        /// <summary>All GraphNodes in this subtree (including inactive).</summary>
        public IEnumerable<GraphNode> GetAllChildren()
            => GetComponentsInChildren<GraphNode>(includeInactive: true);

        /// <summary>
        /// Walk up from <paramref name="from"/> and return the outermost
        /// <see cref="GraphNodePreviewRoot"/> ancestor (including self), or null
        /// if none exists.
        /// </summary>
        public static GraphNodePreviewRoot FindTopMost(Transform from)
        {
            GraphNodePreviewRoot topMost = null;
            var t = from;
            while (t != null)
            {
                if (t.TryGetComponent<GraphNodePreviewRoot>(out var r))
                    topMost = r;
                t = t.parent;
            }
            return topMost;
        }
    }
}
