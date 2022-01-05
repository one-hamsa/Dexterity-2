using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    // NOTE: semantics here refer specifically to the graph, so:
    //. nodes (here) = BaseFields,
    //. edges (here) = UpstreamFields (dependencies).
    //. Don't confuse the nodes mentioned here with Dexterity.Visual.Node.
    public class Graph : MonoBehaviour
    {
        const int throttleOperationsPerFrame = 3000;

        // debug info
        public bool lastSortResult { get; private set; }
        public float lastUpdateAttempt { get; private set; }
        public float lastSuccessfulUpdate { get; private set; }
        public int updateOperations { get; private set; }
        public int updateFrames { get; private set; }

        public bool started { get; set; }
        public bool updating { get; private set; }

        public HashSet<BaseField> nodes { get; } = new HashSet<BaseField>();
        public HashSet<BaseField> nodesForCurrentSortIteration { get; } = new HashSet<BaseField>();
        
        public Dictionary<BaseField, IEnumerable<BaseField>> edges { get; } 
            = new Dictionary<BaseField, IEnumerable<BaseField>>();

        public event Action<int> onGraphColorUpdated;

        // keeps track of visits in topological sort
        HashSet<BaseField> visited = new HashSet<BaseField>();
        // dfs stack for topological sort
        Stack<(bool process, BaseField node)> dfs = new Stack<(bool, BaseField)>();
        // helper map for tracking which node is still on stack to avoid loops
        Dictionary<BaseField, bool> onStack = new Dictionary<BaseField, bool>();
        // "color" map (of islands within the graph) - used for only updating relevant nodes
        Dictionary<BaseField, int> nodeToColor = new Dictionary<BaseField, int>();
        Dictionary<BaseField, int> nextNodeToColor = new Dictionary<BaseField, int>();
        Dictionary<int, int> colorToColorMap = new Dictionary<int, int>();
        Dictionary<int, int> nextColorToColorMap = new Dictionary<int, int>();
        Dictionary<int, bool> dirtyColors = new Dictionary<int, bool>();
        List<int> colorsToReset = new List<int>(8);
        // cached graph data
        protected List<BaseField> sortedNodes = new ListSet<BaseField>();
        protected List<BaseField> sortedNodesCache = new List<BaseField>();

        public void AddNode(BaseField node)
        {
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

            // try removing
            sortedNodes.Remove(node);
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

        // updates the graph (if needed), then invokes the update functions for each field
        void Update()
        {
            if (!started || updating)
                return;

            // ask all nodes to refresh their edges
            RefreshEdges();


            if (IsDirty())
            {
                StartCoroutine(nameof(UpdateGraph));
                return;
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
            // cache - the foreach clause might invoke changes to collection
            sortedNodesCache.Clear();
            foreach (var node in sortedNodes)
                sortedNodesCache.Add(node);

            foreach (var node in sortedNodesCache)
                node.CacheValue();
        }

        public IEnumerable<BaseField> GetByColor(int color)
        {
            foreach (var node in nodes)
                if (nodeToColor.TryGetValue(node, out var c) && color == c)
                    yield return node;
        }

        IEnumerator UpdateGraph()
        {
            updating = true;
            try
            {
                lastUpdateAttempt = Time.unscaledTime;

                updateOperations = 0;
                updateFrames = 0;
                var topSort = TopologicalSort();

                while (topSort.MoveNext())
                {
                    if (++updateOperations % throttleOperationsPerFrame == 0)
                    {
                        // wait a frame
                        yield return null;
                        updateFrames++;
                    }
                }

                if (!lastSortResult)
                {
                    Debug.LogError("Graph sort failed");
                    yield break;
                }

                lastSuccessfulUpdate = Time.unscaledTime;

                // invoke general update
                RefreshNodeValues();
            }
            finally
            {
                updating = false;
            }
        }

        // this is made an iterator in order to allow throttling. sources:
        //. https://stackoverflow.com/questions/20153488/topological-sort-using-dfs-without-recursion
        //. https://stackoverflow.com/questions/56316639/detect-cycle-in-directed-graph-with-non-recursive-dfs
        IEnumerator<BaseField> TopologicalSort()
        {
            // first copy all nodes
            nodesForCurrentSortIteration.Clear();
            foreach (var node in nodes)
                nodesForCurrentSortIteration.Add(node);

            updateOperations = 0;
            var currentColor = -1;

            visited.Clear();
            dfs.Clear();
            onStack.Clear();
            nextNodeToColor.Clear();
            nextColorToColorMap.Clear();

            foreach (var node in nodesForCurrentSortIteration)
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

                    yield return current.node;

                    onStack[current.node] = false;

                    if (current.process)
                    {
                        // remove from sorted if it already exists
                        sortedNodes.Remove(current.node);

                        // finish sorting for n
                        sortedNodes.Add(current.node);
                        nextNodeToColor.Add(current.node, currentColor);
                        continue;
                    }
                    
                    if (visited.Add(current.node))
                    {
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
                            {
                                // this is already a dependency somewhere on the stack, it means we have a cycle
                                lastSortResult = false;
                                yield break;
                            }

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
            colorsToReset.Clear();
            foreach (var color in dirtyColors.Keys)
            {
                if (dirtyColors[color])
                    colorsToReset.Add(color);
            }
            foreach (var color in colorsToReset)
            {
                onGraphColorUpdated?.Invoke(color);
                dirtyColors[color] = false;
            }

            lastSortResult = true;
        }
    }
}
