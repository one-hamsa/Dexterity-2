using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Builtins
{
    /// <summary>
    /// Transform-walk helpers used by <see cref="NodeStateProvider"/> and
    /// <see cref="GraphInputProvider"/> to discover target node(s) per their
    /// <see cref="NodeRefMode"/>. Single source of truth so the two providers
    /// stay consistent.
    /// </summary>
    internal static class NodeRefDiscovery
    {
        /// <summary>
        /// First <typeparamref name="T"/> ancestor strictly above the host node.
        /// (Walks from <paramref name="from"/>'s parent upward — the host itself
        /// is skipped since the provider sits on it.)
        /// </summary>
        public static T FirstParent<T>(Transform from) where T : Component
        {
            // The provider's GO IS the host's GO. We want a node ABOVE the host,
            // so start the walk from the host's parent.
            var t = from.parent;
            while (t != null)
            {
                if (t.TryGetComponent<T>(out var c)) return c;
                t = t.parent;
            }
            return null;
        }

        /// <summary>
        /// Every <typeparamref name="T"/> instance among descendants of <paramref name="from"/>,
        /// stopping descent at the first hit per branch (nested-node boundary).
        /// </summary>
        public static IEnumerable<T> Children<T>(Transform from) where T : Component
        {
            for (var i = 0; i < from.childCount; i++)
            {
                var child = from.GetChild(i);
                if (child.TryGetComponent<T>(out var c))
                {
                    yield return c;
                    // don't descend — that subtree belongs to that nested node
                    continue;
                }
                foreach (var nested in Children<T>(child))
                    yield return nested;
            }
        }

        /// <summary>
        /// Resolve every target a provider should subscribe to, given its current mode.
        /// Used by OnEnable to wire event subscriptions.
        /// </summary>
        public static IEnumerable<T> Resolve<T>(Component provider, NodeRefMode mode, T explicitTarget) where T : Component
        {
            switch (mode)
            {
                case NodeRefMode.Reference:
                    if (explicitTarget != null) yield return explicitTarget;
                    break;
                case NodeRefMode.FirstParent:
                    var parent = FirstParent<T>(provider.transform);
                    if (parent != null) yield return parent;
                    break;
                case NodeRefMode.AnyChild:
                case NodeRefMode.AllChildren:
                    foreach (var c in Children<T>(provider.transform))
                        yield return c;
                    break;
            }
        }
    }
}
