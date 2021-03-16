using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    // NOTE: semantics here refer specifically to the graph, so:
    //. nodes (here) = BaseFields,
    //. edges (here) = UpstreamFields (dependencies).
    //. Don't confuse the nodes mentioned here with Dexterity.Visual.Node.
    public class Graph
    {
        bool dirty;

        // API and user-defined data
        protected HashSet<BaseField> nodes = new HashSet<BaseField>();
        public void AddNode(BaseField node)
        {
            nodes.Add(node);
            edges[node] = node.GetUpstreamFields();
            dirty = true;
        }
        public void RemoveNode(BaseField node)
        {
            nodes.Remove(node);
            dirty = true;
        }

        // cached graph data
        protected List<BaseField> sortedNodes = new List<BaseField>();

        // (hopefully) pre-allocated data structures
        Dictionary<BaseField, HashSet<BaseField>> edges = new Dictionary<BaseField, HashSet<BaseField>>();

        // updates the graph (if needed), then invokes the update functions for each field
        public void Run()
        {
            // ask all nodes to refresh their edges
            RefreshEdges();

            // dynamically calculate edges
            if (!GetEdges())
            {
                Debug.LogError("Graph sort failed");
                return;
            }

            if (dirty)
            {
                // invalidate
                if (!TopologicalSort())
                {
                    Debug.LogError("Graph sort failed");
                    return;
                }

                dirty = false;
                // TODO save debug info about invalidation event (time etc.)
            }

            // invoke update
            RefreshNodeValues();
        }


        void RefreshEdges()
        {
            foreach (var node in nodes)
            {
                node.RefreshReferences();
            }
        }

        void RefreshNodeValues()
        {
            foreach (var node in sortedNodes)
            {
                node.CacheValue();
            }
        }


        bool GetEdges()
        {
            foreach (var node in nodes)
            {
                if (node.isDirty)
                {
                    dirty = true;
                    break;
                }
            }
            return true;
        }


        HashSet<BaseField> visited = new HashSet<BaseField>();
        Stack<(bool, BaseField)> dfs = new Stack<(bool, BaseField)>();

        // https://stackoverflow.com/questions/20153488/topological-sort-using-dfs-without-recursion
        bool TopologicalSort()
        {
            // TODO check for loops
            sortedNodes.Clear();
            visited.Clear();
            dfs.Clear();

            foreach (var node in nodes)
            {
                if (!visited.Contains(node))
                {
                    dfs.Push((false, node));
                }
                while (dfs.Count > 0)
                {
                    var (b, n) = dfs.Pop();
                    if (b)
                    {
                        sortedNodes.Add(n);
                        // this is here just to save another iteration
                        n.ClearDirty();
                        continue;
                    }
                    visited.Add(n);
                    dfs.Push((true, n));
                    if (!edges.TryGetValue(n, out var refs))
                        continue;
                    foreach (var son in refs)
                    {
                        if (!visited.Contains(son))
                        {
                            dfs.Push((false, son));
                        }
                    }
                }
            }

            // TODO check validity!
            return true;
        }
    }
}
