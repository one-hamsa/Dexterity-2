using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static OneHamsa.Dexterity.StateFunction;

namespace OneHamsa.Dexterity
{
    public interface IStepList : IHasStates
    {
        private static Stack<(Step step, int depth)> depthStack = new();
        int GetLastEvaluationResult();

        List<Step> steps { get; }

        void InitializeSteps() {
            foreach (var step in steps) {
                step.Initialize();
            }
        }
        
        private static int GetValue(List<(int field, int value)> mask, int field)
        {
            foreach (var pair in mask)
                if (pair.field == field)
                    return pair.value;
            return FieldNode.emptyFieldValue;
        }

        static int Evaluate(List<(Step step, int depth)> cache, List<(int,int)> mask) {
            var conditionMetDepth = -1;
            foreach (var (step, depth) in cache) {
                if (conditionMetDepth < depth - 1) {
                    continue;
                }

                switch (step.type) {
                    case Step.Type.Condition:
                        var res = GetValue(mask, step.GetConditionFieldID()) == step.condition_fieldValue;
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

        // XXX there's a bug in C#?! - if I cast the object to be IStepList and call a method
        //. whose signature (here) is IHasStates.X() - it calls the parent method, not this one.
        //. that's why "StepList" is added here, and the code become more convoluted. sorry.
        IEnumerable<string> GetStepListFieldNames()
        {
            foreach (var step in steps) {
                if (step.type == Step.Type.Condition)
                    yield return step.condition_fieldName;
                else if (step.type == Step.Type.Reference && step.reference_stateFunction != null) {
                    foreach (var state in (step.reference_stateFunction as IStepList).GetStepListFieldNames())
                        yield return state;
                }
            }
        }

        IEnumerable<string> GetStepListStateNames()
        {
            foreach (var step in steps) {
                if (step.type == Step.Type.Result)
                    yield return step.result_stateName;
                else if (step.type == Step.Type.Reference && step.reference_stateFunction != null) {
                    foreach (var state in (step.reference_stateFunction as IStepList).GetStepListStateNames())
                        yield return state;
                }
            }
        }

        bool HasFallback() {
            foreach (var step in steps) {
                if (step.parent == -1) {
                    if (step.type == Step.Type.Result)
                        return true;
                    if (step.type == Step.Type.Reference && step.reference_stateFunction != null) {
                        if ((step.reference_stateFunction as IStepList).HasFallback())
                            return true;
                    }
                }
            }
            return false;
        }

        void PopulateStepCache(List<(Step step, int depth)> cache)
        {
            cache.Clear();
            cache.AddRange(EnumerateTreeStepsDFS());
        }

        IEnumerable<(Step step, int depth)> EnumerateTreeStepsDFS() {
            var tree = ListToTree();
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
        Dictionary<Step, List<Step>> ListToTree() {
            // add root step
            var lastVisited = Step.Root;
            
            var idToStep = new Dictionary<int, Step>();
            idToStep.Add(Step.Root.id, Step.Root);

            foreach (var step in steps) {
                idToStep.Add(step.id, step);
            }

            var tree = new Dictionary<Step, List<Step>>();

            foreach (var step in steps) {
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
        string GetTreeDebugString() {
            var sb = new System.Text.StringBuilder();

            foreach (var (step, depth) in EnumerateTreeStepsDFS()) {
                sb.Append($"{new string('-', depth)}[{step.id}] {step.type}\n");
            }
            
            return sb.ToString();
        }
    }
}