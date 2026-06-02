using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// Search-window provider for adding sources to a Dexterity graph. Displays a
    /// two-level tree: Source / Operator → concrete types (reflected via
    /// TypeCache, suffix stripped). Selecting an entry adds the component to the
    /// graph's host GameObject.
    ///
    /// Wired via <c>GraphView.nodeCreationRequest</c> — fires on Spacebar or via
    /// the "Create Node" context menu entry.
    /// </summary>
    internal class DexterityAddSourceSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        public DexterityGraphView view;
        public Vector2 spawnGraphPos;

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var entries = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Add Source"), 0),
                new SearchTreeGroupEntry(new GUIContent("Source"), 1),
            };

            foreach (var t in TypeCache.GetTypesDerivedFrom<GraphSource>()
                                 .Where(t => !t.IsAbstract)
                                 .OrderBy(t => t.Name))
            {
                entries.Add(new SearchTreeEntry(new GUIContent(
                        DexterityGraphView.StripSuffix(t.Name, "Source")))
                    { level = 2, userData = t });
            }

            entries.Add(new SearchTreeGroupEntry(new GUIContent("Operator"), 1));
            foreach (var t in TypeCache.GetTypesDerivedFrom<GraphOperator>()
                                 .Where(t => !t.IsAbstract)
                                 .OrderBy(t => t.Name))
            {
                entries.Add(new SearchTreeEntry(new GUIContent(
                        DexterityGraphView.StripSuffix(t.Name, "Operator")))
                    { level = 2, userData = t });
            }

            return entries;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            if (view == null) return false;
            if (entry.userData is not System.Type type) return false;
            view.AddSourceOfTypeAt(type, spawnGraphPos);
            return true;
        }
    }
}
