using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System.Text;

namespace OneHamsa.Dexterity.Visual
{
    [Serializable]
    public class StateFunction : ScriptableObject
    {
        public List<NodeLinkData> NodeLinks = new List<NodeLinkData>();
        public List<ConditionNodeData> ConditionNodeData = new List<ConditionNodeData>();
        public List<DecisionNodeData> DecisionNodeData = new List<DecisionNodeData>();
        public List<ExposedProperty> ExposedProperties = new List<ExposedProperty>();

        public string ErrorString { get; private set; }

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

            // start at entry point
            var entries = ConditionNodeData.Where(n => n.EntryPoint);
            if (entries.Count() != 1)
            {
                ErrorString = "Should have exactly one entry point";
                return false;
            }

            var decisionRefCount = new Dictionary<string, int>();
            foreach (var desc in cachedDecisions.Values)
            {
                if (string.IsNullOrWhiteSpace(desc.State))
                {
                    ErrorString = "State must be filled for every decision";
                    return false;
                }
                decisionRefCount[desc.NodeGUID] = 0;
            }

            var queue = new Queue<ConditionNodeData>(new[] { entries.First() });
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (string.IsNullOrWhiteSpace(current.Field))
                {
                    ErrorString = "Field must be selected for every condition";
                    return false;
                }

                if (!current.EntryPoint && !cachedEdges.ContainsKey(current.NodeGUID))
                {
                    ErrorString = $"All conditions must be referenced ({current.Field})";
                    return false;
                }

                var globalFieldOrNull = Manager.Instance.GetFieldDefinition(current.Field);
                if (globalFieldOrNull == null)
                {
                    ErrorString = $"Field {current.Field} not found in Manager";
                    return false;
                }
                var globalField = globalFieldOrNull.Value;

                var edgeCount = 0;
                if (cachedEdges.TryGetValue(current.NodeGUID, out var cachedLst))
                    edgeCount = cachedLst.Count;

                if ((globalField.Type == Node.FieldType.Boolean && edgeCount != 2)
                    || (globalField.Type == Node.FieldType.Enum && edgeCount != globalField.EnumValues.Length))
                {
                    ErrorString = $"All condition fields must point somewhere ({current.Field})";
                    return false;
                }

                foreach (var targetUUID in cachedEdges[current.NodeGUID].Values)
                {
                    if (cachedConditions.TryGetValue(targetUUID, out var cond))
                        queue.Enqueue(cond);
                    else if (decisionRefCount.ContainsKey(targetUUID))
                    {
                        decisionRefCount[targetUUID]++;
                    }
                    else
                    {
                        ErrorString = "Invalid reference from condition";
                        return false;
                    }
                }
            }

            if (decisionRefCount.Values.Any(d => d == 0))
            {
                ErrorString = "All decisions must be referenced";
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

                if (!fields.ContainsKey(node.Field))
                {
                    Debug.LogError($"{node.Field} is required for evaluating state function {name}");
                    result = null;
                    break;
                }

                var gField = Manager.Instance.GetFieldDefinition(node.Field).Value;
                var value = fields[node.Field];
                string target;
                var branches = cachedEdges[node.NodeGUID];
                if (gField.Type == Node.FieldType.Boolean)
                {
                    if (value == 1)
                        target = branches["true"];
                    else  // value == 0
                        target = branches["false"];
                }
                else // == Enum
                {
                    target = branches[gField.EnumValues[value]];
                }

                if (cachedDecisions.TryGetValue(target, out var decision))
                {
                    result = decision.State;
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
            foreach (var cond in ConditionNodeData)
            {
                cachedConditions[cond.NodeGUID] = cond;
                cachedFields.Add(cond.Field);
            }
        }

        Dictionary<string, DecisionNodeData> cachedDecisions = new Dictionary<string, DecisionNodeData>();
        HashSet<string> cachedStates = new HashSet<string>();
        void BuildDecisions()
        {
            cachedDecisions.Clear();
            foreach (var desc in DecisionNodeData)
            {
                cachedDecisions[desc.NodeGUID] = desc;
                cachedStates.Add(desc.State);
            }
        }

        Dictionary<string, Dictionary<string, string>> cachedEdges = new Dictionary<string, Dictionary<string, string>>();
        void BuildEdges()
        {
            foreach (var d in cachedEdges.Values)
                d.Clear();

            foreach (var link in NodeLinks)
            {
                if (!cachedEdges.ContainsKey(link.BaseNodeGUID))
                    cachedEdges[link.BaseNodeGUID] = new Dictionary<string, string>();

                cachedEdges[link.BaseNodeGUID].Add(link.BasePort, link.TargetNodeGUID);
            }
        }

        ConditionNodeData cachedEntryPoint;
        void FindEntryPoint()
        {
            foreach (var cond in ConditionNodeData)
            {
                if (cond.EntryPoint)
                {
                    cachedEntryPoint = cond;
                    return;
                }
            }
            Debug.LogWarning($"Could not find entry point for state function {name}");
        }
    }
}