using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    using Utilities;
    
    // NOTE: semantics here refer specifically to the graph, so:
    //. nodes (here) = BaseFields,
    //. edges (here) = UpstreamFields (dependencies).
    //. Don't confuse the nodes mentioned here with Dexterity.Visual.Node.
    [DefaultExecutionOrder(Manager.graphExecutionPriority)]
    public class Graph : MonoBehaviour
    {
        public const int throttleOperationsPerFrame = 3000;

        // debug info
        public float lastUpdateAttempt { get; private set; }
        public float lastSuccessfulUpdate { get; private set; }
        public int updateOperations { get; private set; }
        public int updatedNodes { get; private set; }
        public int updateFrames { get; private set; }
        public bool lastSortResult => cyclePoints.Count == 0;
        public readonly HashSet<BaseField> cyclePoints = new();

        public bool started { get; set; }
        public bool updating { get; private set; }

        public HashSet<BaseField> nodes { get; } = new();
        public HashSet<BaseField> nodesForCurrentSortIteration { get; } = new();
        
        public Dictionary<BaseField, IEnumerable<BaseField>> edges { get; } = new();

        public event Action onGraphUpdated;
        public event Action<int> onGraphColorUpdated;

        /// <summary>
        /// list of topologically-sorted nodes, used for invoking updates in order. public visibility for editor.
        /// CAUTION: don't read when updating, this might return stale values.
        /// </summary>
        public List<BaseField> sortedNodes = new ListSet<BaseField>();
        private List<BaseField> sortedNodesCache = new();

        /// <summary>
        /// "color" map (of islands within the graph) - used for updating only dirty nodes. public visibility for editor.
        /// CAUTION: don't read when updating, this might return stale values.
        /// </summary>
        public Dictionary<BaseField, int> nodeToColor = new();
        private Dictionary<BaseField, int> nextNodeToColor = new();

        /// <summary>
        /// map of color to color, used for clusters (disjointed set / union-find algo). public visibility for editor.
        /// CAUTION: don't read when updating, this might return stale values.
        /// </summary>
        public Dictionary<int, int> colorToColorMap = new();
        private Dictionary<int, int> nextColorToColorMap = new();

        // keeps track of visits in topological sort
        private HashSet<BaseField> visited = new();
        // dfs stack for topological sort
        private Stack<(bool process, BaseField node)> dfs = new();
        // helper map for tracking which node is still on stack to avoid loops
        private Dictionary<BaseField, bool> onStack = new();
        private Dictionary<int, bool> dirtyColors = new();
        private List<int> colorsToReset = new(8);
        private int dirtyIncrement = 0;
        private int lastDirtyUpdate = -1;

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
            dirtyIncrement++;
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
        public void Refresh()
        {
            if (!started || updating)
                return;

            // ask all nodes to refresh their edges
            RefreshEdges();


            if (lastDirtyUpdate != dirtyIncrement)
            {
                StartCoroutine(nameof(UpdateGraph));
                return;
            }

            // invoke update
            RefreshNodeValues();
        }

        // disjointed-set-style search
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                updatedNodes = 0;
                updateFrames = 1;
                var startDirtyIncrement = dirtyIncrement;
                var topSort = TopologicalSort();

                while (topSort.MoveNext())
                {
                    if (startDirtyIncrement != dirtyIncrement)
                    {
                        // dirty increment changed, restart topological sort (calculate cumulative update time)
                        startDirtyIncrement = dirtyIncrement;
                        topSort = TopologicalSort();
                    }
                    if (++updateOperations % throttleOperationsPerFrame == 0)
                    {
                        // wait a frame
                        yield return null;
                        updateFrames++;
                    }
                }

                lastSuccessfulUpdate = Time.unscaledTime;

                // invoke general update
                RefreshNodeValues();
                
                onGraphUpdated?.Invoke();

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

            var currentColor = -1;

            visited.Clear();
            dfs.Clear();
            onStack.Clear();
            nextNodeToColor.Clear();
            nextColorToColorMap.Clear();
            cyclePoints.Clear();

            foreach (var node in nodesForCurrentSortIteration)
            {
                // skip nodes with non-dirty colors
                if (!IsDirty(node))
                    continue;

                // keep track of how many nodes we've updated
                updatedNodes++;

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
                    var current = dfs.Pop();
                    
                    // it's possible to get a dependency to a node that was removed
                    if (!nodesForCurrentSortIteration.Contains(current.node))
                        continue;

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
                                cyclePoints.Add(son);
                                
                                Debug.LogError($"Graph sort: found cycle at [{son.ToShortString()}]", son.context);
                                // try to recover
                            }
                            else
                            {
                                // we visited this node, copy its color
                                nextColorToColorMap[currentColor] = nextNodeToColor[son];
                            }
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

            lastDirtyUpdate = dirtyIncrement;
        }
    }
}
