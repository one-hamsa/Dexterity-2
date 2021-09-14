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
        // debug info
        public bool lastSortResult { get; private set; }
        public float lastUpdateAttempt { get; private set; }
        public float lastSuccessfulUpdate { get; private set; }
        public float updateOperations { get; private set; }

        public bool started { get; set; }

        public List<BaseField> nodes { get; } = new List<BaseField>();
        
        public ListMap<BaseField, IEnumerable<BaseField>> edges { get; } 
            = new ListMap<BaseField, IEnumerable<BaseField>>();

        public event Action<int> onGraphColorUpdated;

        // keeps track of visits in topological sort
        List<BaseField> visited = new List<BaseField>();
        // dfs stack for topological sort
        Stack<(bool process, BaseField node)> dfs = new Stack<(bool, BaseField)>();
        // helper map for tracking which node is still on stack to avoid loops
        ListMap<BaseField, bool> onStack = new ListMap<BaseField, bool>();
        // "color" map (of islands within the graph) - used for only updating relevant nodes
        ListMap<BaseField, int> nodeToColor = new ListMap<BaseField, int>();
        ListMap<BaseField, int> nextNodeToColor = new ListMap<BaseField, int>();
        ListMap<int, int> colorToColorMap = new ListMap<int, int>();
        ListMap<int, int> nextColorToColorMap = new ListMap<int, int>();

        ListMap<int, bool> dirtyColors = new ListMap<int, bool>();

        public void AddNode(BaseField node)
        {
            if (!nodes.Contains(node))
                nodes.Add(node);

            edges[node] = node.GetUpstreamFields();

            SetDirty(node);
            foreach (var n in node.GetUpstreamFields())
                SetDirty(n);
        }
        public void RemoveNode(BaseField node)
        {
            nodes.Remove(node);
            edges.Remove(node);

            SetDirty(node);
            foreach (var n in node.GetUpstreamFields())
                SetDirty(n);
        }
        public void SetDirty(BaseField field)
        {
            if (!nodeToColor.TryGetValue(field, out var color))
            {
                nodeToColor[field] = color = -1;
            }

            if (!colorToColorMap.TryGetValue(color, out var colorToColor))
            {
                colorToColorMap[color] = colorToColor = color;
            }

            dirtyColors[color] = true;
            dirtyColors[colorToColor] = true;
        }

        // cached graph data
        protected List<BaseField> sortedNodes = new List<BaseField>();

        // updates the graph (if needed), then invokes the update functions for each field
        public void Run()
        {
            if (!started)
                return;

            // ask all nodes to refresh their edges
            RefreshEdges();


            if (IsDirty())
            {
                // invalidate
                lastUpdateAttempt = Time.time;
                if (!(lastSortResult = TopologicalSort()))
                {
                    Debug.LogError("Graph sort failed");
                    return;
                }

                lastSuccessfulUpdate = Time.time;
            }

            // invoke update
            RefreshNodeValues();
        }

        // if any is dirty, return true
        bool IsDirty()
        {
            foreach (var dirty in dirtyColors.Values)
                if (dirty)
                    return true;

            return false;
        }

        // disjointed-set-style search
        private bool IsDirty(BaseField node)
        {
            if (!nodeToColor.TryGetValue(node, out var color))
                // couldn't find, assume dirty
                return true;

            var candidateColor = color;
            while (colorToColorMap[color] != candidateColor)
            {
                // compress and keep searching
                candidateColor = colorToColorMap[candidateColor] = colorToColorMap[color];
            }

            if (!dirtyColors.TryGetValue(colorToColorMap[color], out var dirty))
                // couldn't find, assume dirty
                return true;

            return dirty;
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

        public IEnumerable<BaseField> GetByColor(int color)
        {
            foreach (var node in nodes)
                if (nodeToColor.TryGetValue(node, out var c) && color == c)
                    yield return node;
        }

        // https://stackoverflow.com/questions/20153488/topological-sort-using-dfs-without-recursion
        //. and https://stackoverflow.com/questions/56316639/detect-cycle-in-directed-graph-with-non-recursive-dfs
        bool TopologicalSort()
        {
            updateOperations = 0;
            var currentColor = -1;

            sortedNodes.Clear();
            visited.Clear();
            dfs.Clear();
            onStack.Clear();
            nextNodeToColor.Clear();
            nextColorToColorMap.Clear();

            foreach (var node in nodes)
            {
                // skip nodes with non-dirty colors
                if (!IsDirty(node))
                    continue;

                // only add nodes we hadn't visted yet
                if (!visited.Contains(node))
                {
                    dfs.Push((false, node));
                    currentColor++;
                    // for now, point the color to itself
                    nextColorToColorMap[currentColor] = currentColor;
                }
                while (dfs.Count > 0)
                {
                    updateOperations++;
                    var current = dfs.Pop();
                    onStack[current.node] = false;

                    if (current.process)
                    {
                        // finish sorting for n
                        sortedNodes.Add(current.node);
                        nextNodeToColor.Add(current.node, currentColor);
                        continue;
                    }
                    
                    if (!visited.Contains(current.node))
                    {
                        visited.Add(current.node);

                        // first-time visit, add to stack before pushing all dependencies
                        dfs.Push((true, current.node));
                        // also, mark as "on stack". this will help track down cycles.
                        //. if we later find this as a dependency WHILE this is still on stack,
                        //. it means we have a cycle.
                        onStack[current.node] = true;
                    }

                    // push all dependencies of n on top of the stack
                    if (!edges.TryGetValue(current.node, out var refs))
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
                        else
                        {
                            if (onStack.TryGetValue(son, out var sonOnStack) && sonOnStack)
                                // this is already a dependency somewhere on the stack, it means we have a cycle
                                return false;

                            // we visited this node, copy its color
                            nextColorToColorMap[currentColor] = nextNodeToColor[son];
                        }
                    }
                }
            }

            // swap pointers
            (nodeToColor, nextNodeToColor) = (nextNodeToColor, nodeToColor);
            (colorToColorMap, nextColorToColorMap) = (nextColorToColorMap, colorToColorMap);
            // reset dirty colors
            foreach (var kv in dirtyColors)
            {
                if (kv.Value == true)
                {
                    onGraphColorUpdated?.Invoke(kv.Key);
                    dirtyColors[kv.Key] = false;
                }
            }

            return true;
        }
    }
}
