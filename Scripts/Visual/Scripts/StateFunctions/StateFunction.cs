using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    [Serializable]
    [CreateAssetMenu(fileName = "StateFunction", menuName = "Dexterity/State Function", order = 100)]
    public class StateFunction : ScriptableObject
    {
        public List<NodeLinkData> nodeLinks = new List<NodeLinkData>();
        public List<ConditionNodeData> conditionNodeData = new List<ConditionNodeData>();
        public List<DecisionNodeData> decisionNodeData = new List<DecisionNodeData>();

        public string errorString { get; private set; }

        public void Initialize()
        {
            InvalidateCache();
        }

        Dictionary<string, string> cache = new Dictionary<string, string>();
        public void InvalidateCache()
        {
            cache.Clear();
            BuildConditions();
            BuildDecisions();
            BuildEdges();
            FindEntryPoint();
        }

        /**
         * Validates graph to ensure its integrity (no missing references or floating states)
         */
        public bool Validate()
        {
            InvalidateCache();
            errorString = "";

            // start at entry point
            var entries = conditionNodeData.Where(n => n.entryPoint);
            if (entries.Count() != 1)
            {
                errorString = "Should have exactly one entry point";
                return false;
            }

            var decisionRefCount = new Dictionary<string, int>();
            foreach (var desc in cachedDecisions.Values)
            {
                if (string.IsNullOrWhiteSpace(desc.state))
                {
                    errorString = "State must be filled for every decision";
                    return false;
                }
                decisionRefCount[desc.nodeGUID] = 0;
            }

            var queue = new Queue<ConditionNodeData>(new[] { entries.First() });
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (string.IsNullOrWhiteSpace(current.field))
                {
                    errorString = "Field must be selected for every condition";
                    return false;
                }

                if (!current.entryPoint && !cachedEdges.ContainsKey(current.nodeGUID))
                {
                    errorString = $"All conditions must be referenced ({current.field})";
                    return false;
                }

                var globalFieldOrNull = Manager.Instance.GetFieldDefinition(current.field);
                if (globalFieldOrNull == null)
                {
                    errorString = $"Field {current.field} not found in Manager";
                    return false;
                }
                var globalField = globalFieldOrNull.Value;

                var edgeCount = 0;
                if (cachedEdges.TryGetValue(current.nodeGUID, out var cachedLst))
                    edgeCount = cachedLst.Count;

                if ((globalField.type == Node.FieldType.Boolean && edgeCount != 2)
                    || (globalField.type == Node.FieldType.Enum && edgeCount != globalField.enumValues.Length))
                {
                    errorString = $"All condition fields must point somewhere ({current.field})";
                    return false;
                }

                foreach (var targetUUID in cachedEdges[current.nodeGUID].Values)
                {
                    if (cachedConditions.TryGetValue(targetUUID, out var cond))
                        queue.Enqueue(cond);
                    else if (decisionRefCount.ContainsKey(targetUUID))
                    {
                        decisionRefCount[targetUUID]++;
                    }
                    else
                    {
                        errorString = "Invalid reference from condition";
                        return false;
                    }
                }
            }

            if (decisionRefCount.Values.Any(d => d == 0))
            {
                errorString = "All decisions must be referenced";
                return false;
            }

            return true;
        }


        HashSet<ConditionNodeData> visited = new HashSet<ConditionNodeData>();
        /**
         * Evaluates a group of fields and returns a final state string. 
         * Assumes Validate() as called and returned true - not checking for errors
         */
        public string Evaluate(Dictionary<string, int> fields)
        {
            // try retrieving cached value
            var cacheId = GetCacheIdentifier(fields);
            if (cacheId != null && cache.TryGetValue(cacheId, out var result))
            {
                return result;
            }

            var node = cachedEntryPoint;

            visited.Clear();
            while (true)
            {
                if (visited.Contains(node))
                {
                    Debug.LogError($"Loop detected in state function {name}");
                    result = null;
                    break;
                }
                visited.Add(node);

                if (!fields.ContainsKey(node.field))
                {
                    Debug.LogError($"{node.field} is required for evaluating state function {name}");
                    result = null;
                    break;
                }

                var gField = Manager.Instance.GetFieldDefinition(node.field).Value;
                var value = fields[node.field];
                string target;
                var branches = cachedEdges[node.nodeGUID];
                if (gField.type == Node.FieldType.Boolean)
                {
                    if (value == 1)
                        target = branches["true"];
                    else  // value == 0
                        target = branches["false"];
                }
                else // == Enum
                {
                    target = branches[gField.enumValues[value]];
                }

                if (cachedDecisions.TryGetValue(target, out var decision))
                {
                    result = decision.state;
                    break;
                }

                node = cachedConditions[target];
            }

            if (cacheId != null)
                cache[cacheId] = result;
            return result;
        }

        string GetCacheIdentifier(Dictionary<string, int> fields)
        {
            // TODO
            return null;
        }


        public HashSet<string> GetStates() => cachedStates;
        public HashSet<string> GetFields() => cachedFields;

        Dictionary<string, ConditionNodeData> cachedConditions = new Dictionary<string, ConditionNodeData>();
        HashSet<string> cachedFields = new HashSet<string>();
        void BuildConditions()
        {
            cachedConditions.Clear();
            foreach (var cond in conditionNodeData)
            {
                cachedConditions[cond.nodeGUID] = cond;
                cachedFields.Add(cond.field);
            }
        }

        Dictionary<string, DecisionNodeData> cachedDecisions = new Dictionary<string, DecisionNodeData>();
        HashSet<string> cachedStates = new HashSet<string>();
        void BuildDecisions()
        {
            cachedDecisions.Clear();
            foreach (var desc in decisionNodeData)
            {
                cachedDecisions[desc.nodeGUID] = desc;
                cachedStates.Add(desc.state);
            }
        }

        Dictionary<string, Dictionary<string, string>> cachedEdges = new Dictionary<string, Dictionary<string, string>>();
        void BuildEdges()
        {
            foreach (var d in cachedEdges.Values)
                d.Clear();

            foreach (var link in nodeLinks)
            {
                if (!cachedEdges.ContainsKey(link.baseNodeGUID))
                    cachedEdges[link.baseNodeGUID] = new Dictionary<string, string>();

                cachedEdges[link.baseNodeGUID].Add(link.basePort, link.targetNodeGUID);
            }
        }

        ConditionNodeData cachedEntryPoint;
        void FindEntryPoint()
        {
            foreach (var cond in conditionNodeData)
            {
                if (cond.entryPoint)
                {
                    cachedEntryPoint = cond;
                    return;
                }
            }
            //Debug.LogWarning($"Could not find entry point for state function {name}");
        }
    }
}