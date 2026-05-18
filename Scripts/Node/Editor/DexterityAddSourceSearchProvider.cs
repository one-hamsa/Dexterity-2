using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// Search-window provider for adding sources to a Dexterity graph. Displays a
    /// two-level tree: Provider / Aggregator → concrete types (reflected via
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
                new SearchTreeGroupEntry(new GUIContent("Provider"), 1),
            };

            foreach (var t in TypeCache.GetTypesDerivedFrom<GraphStateProvider>()
                                 .Where(t => !t.IsAbstract)
                                 .OrderBy(t => t.Name))
            {
                entries.Add(new SearchTreeEntry(new GUIContent(
                        DexterityGraphView.StripSuffix(t.Name, "Provider")))
                    { level = 2, userData = t });
            }

            entries.Add(new SearchTreeGroupEntry(new GUIContent("Aggregator"), 1));
            foreach (var t in TypeCache.GetTypesDerivedFrom<GraphAggregator>()
                                 .Where(t => !t.IsAbstract)
                                 .OrderBy(t => t.Name))
            {
                entries.Add(new SearchTreeEntry(new GUIContent(
                        DexterityGraphView.StripSuffix(t.Name, "Aggregator")))
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
