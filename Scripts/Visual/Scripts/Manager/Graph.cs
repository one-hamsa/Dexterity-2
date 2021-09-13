using OneHumus.Data;
using System;
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
        protected bool dirty;

        // debug info
        public bool lastSortResult { get; private set; }
        public float lastUpdateAttempt { get; private set; }
        public float lastSuccessfulUpdate { get; private set; }
        public float updateOperations { get; private set; }

        public bool started { get; set; }

        public List<BaseField> nodes { get; } = new List<BaseField>();
        
        public ListMap<BaseField, IEnumerable<BaseField>> edges { get; } 
            = new ListMap<BaseField, IEnumerable<BaseField>>();

        public void AddNode(BaseField node)
        {
            if (!nodes.Contains(node))
                nodes.Add(node);

            edges[node] = node.GetUpstreamFields();
            dirty = true;
        }
        public void RemoveNode(BaseField node)
        {
            nodes.Remove(node);
            edges.Remove(node);
            dirty = true;
        }
        public bool SetDirty() => dirty = true;

        // cached graph data
        protected List<BaseField> sortedNodes = new List<BaseField>();

        // updates the graph (if needed), then invokes the update functions for each field
        public void Run()
        {
            if (!started)
                return;

            // ask all nodes to refresh their edges
            RefreshEdges();

            if (dirty)
            {
                // invalidate
                lastUpdateAttempt = Time.time;
                if (!(lastSortResult = TopologicalSort()))
                {
                    Debug.LogError("Graph sort failed");
                    return;
                }

                dirty = false;
                lastSuccessfulUpdate = Time.time;
            }

            // invoke update
            RefreshNodeValues();
        }


        void RefreshEdges()
        {
            foreach (var node in nodes)
            {
                try
                {
                    node.RefreshReferences();
                }
                catch (Exception e)
                {
                    Debug.LogException(e, Node.ByField(node));
                }
            }
        }

        void RefreshNodeValues()
        {
            foreach (var node in sortedNodes)
            {
                node.CacheValue();
            }
        }

        HashSet<BaseField> visited = new HashSet<BaseField>();
        Stack<(bool, BaseField)> dfs = new Stack<(bool, BaseField)>();
        ListMap<BaseField, bool> onStack = new ListMap<BaseField, bool>();

        // https://stackoverflow.com/questions/20153488/topological-sort-using-dfs-without-recursion
        //. and https://stackoverflow.com/questions/56316639/detect-cycle-in-directed-graph-with-non-recursive-dfs
        bool TopologicalSort()
        {
            updateOperations = 0;

            sortedNodes.Clear();
            visited.Clear();
            dfs.Clear();
            onStack.Clear();

            foreach (var node in nodes)
            {
                if (!visited.Contains(node))
                {
                    dfs.Push((false, node));
                }
                while (dfs.Count > 0)
                {
                    updateOperations++;
                    var (b, n) = dfs.Pop();
                    onStack[n] = false;

                    if (b)
                    {
                        // finish sorting for n
                        sortedNodes.Add(n);
                        continue;
                    }
                    
                    if (visited.Add(n))
                    {
                        // first-time visit, add to stack before pushing all dependencies
                        dfs.Push((true, n));
                        // also, mark as "on stack". this will help track down cycles.
                        //. if we later find this as a dependency WHILE this is still on stack,
                        //. it means we have a cycle.
                        onStack[n] = true;
                    }

                    // push all dependencies of n on top of the stack
                    if (!edges.TryGetValue(n, out var refs))
                        // no dependencies, no need to push anything
                        continue;

                    foreach (var son in refs)
                    {
                        if (son == null)
                        {
                            // this might happen if a field registered uninitialized upstream fields
                            continue;
                        }
                        if (!visited.Contains(son))
                        {
                            dfs.Push((false, son));
                        }
                        else if (onStack.TryGetValue(son, out var sonOnStack) && sonOnStack)
                            // this is already a dependency somewhere on the stack, it means we have a cycle
                            return false;
                    }
                }
            }

            return true;
        }
    }
}
