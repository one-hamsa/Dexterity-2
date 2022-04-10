using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static OneHamsa.Dexterity.Visual.StateFunction;

namespace OneHamsa.Dexterity.Visual
{
    public interface IStepList
    {
        List<Step> steps { get; }
    }

    public static class IStepListExtensions {
        private static HashSet<string> namesSet = new HashSet<string>();
        private static Stack<(Step step, int depth)> depthStack = new Stack<(Step step, int depth)>();

        public static int Evaluate(this IStepList stepList, StepEvaluationCache cache, FieldMask mask) {
            var conditionMetDepth = -1;
            foreach (var (step, depth) in cache) {
                if (conditionMetDepth < depth - 1) {
                    continue;
                }

                switch (step.type) {
                    case Step.Type.Condition:
                        var res = mask.GetValue(step.GetConditionFieldID()) == step.condition_fieldValue;
                        if (step.condition_negate) {
                            res = !res;
                        }
                        if (res) {
                            conditionMetDepth = depth;
                        }
                        break;
                    
                    case Step.Type.Result:
                        return step.GetResultStateID();

                    case Step.Type.Reference:
                        var value = step.reference_stateFunction.Evaluate(mask);
                        if (value != emptyStateId) {
                            return value;
                        }
                        break;
                }
            }
            return emptyStateId;
        }

        public static IEnumerable<string> GetFieldNames(this IStepList stepList)
        {
            foreach (var step in stepList.steps) {
                if (step.type == Step.Type.Condition)
                    yield return step.condition_fieldName;
                else if (step.type == Step.Type.Reference && step.reference_stateFunction != null) {
                    foreach (var state in GetFieldNames(step.reference_stateFunction))
                        yield return state;
                }
            }
        }

        public static IEnumerable<string> GetStates(this IStepList stepList)
        {
            foreach (var step in stepList.steps) {
                if (step.type == Step.Type.Result)
                    yield return step.result_stateName;
                else if (step.type == Step.Type.Reference && step.reference_stateFunction != null) {
                    foreach (var state in GetStates(step.reference_stateFunction))
                        yield return state;
                }
            }
        }

        public static bool HasFallback(this IStepList stepList) {
            foreach (var step in stepList.steps) {
                if (step.parent == -1) {
                    if (step.type == Step.Type.Result)
                        return true;
                    if (step.type == Step.Type.Reference && step.reference_stateFunction != null) {
                        if (HasFallback(step.reference_stateFunction))
                            return true;
                    }
                }
            }
            return false;
        }

        public static StepEvaluationCache BuildStepCache(this IStepList stepList)
            => new StepEvaluationCache(EnumerateTreeStepsDFS(stepList));

        public static IEnumerable<(Step step, int depth)> EnumerateTreeStepsDFS(this IStepList stepList) {
            var tree = ListToTree(stepList);
            depthStack.Clear();
            foreach (var step in tree.Keys) {
                if (step.isRoot)
                    depthStack.Push((step, -1));
            }

            while (depthStack.Count > 0) {
                var (step, depth) = depthStack.Pop();
                if (!step.isRoot)
                    yield return (step, depth);
                if (tree.ContainsKey(step)) {
                    foreach (var child in tree[step].Reverse<Step>()) {
                        depthStack.Push((child, depth + 1));
                    }
                }
            }
        }
        
        // convert list with parent ids to tree structure where each node has a list of children
        private static Dictionary<Step, List<Step>> ListToTree(IStepList stepList) {
            // add root step
            var lastVisited = Step.Root;
            
            var idToStep = new Dictionary<int, Step>();
            idToStep.Add(Step.Root.id, Step.Root);

            foreach (var step in stepList.steps) {
                idToStep.Add(step.id, step);
            }

            var tree = new Dictionary<Step, List<Step>>();

            foreach (var step in stepList.steps) {
                if (!idToStep.ContainsKey(step.parent)) {
                    Debug.LogError($"Step {step.id} has invalid parent {step.parent}");
                    continue;
                }
                var parent = idToStep[step.parent];
                if (!tree.ContainsKey(parent)) {
                    tree.Add(parent, new List<Step>());
                }
                tree[parent].Add(step);
            }

            return tree;
        }
        public static string GetTreeDebugString(this IStepList stepList) {
            var sb = new System.Text.StringBuilder();

            foreach (var (step, depth) in EnumerateTreeStepsDFS(stepList)) {
                sb.Append($"{new string('-', depth)}[{step.id}] {step.type}\n");
            }
            
            return sb.ToString();
        }
    }
}