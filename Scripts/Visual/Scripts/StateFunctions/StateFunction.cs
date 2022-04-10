using GraphProcessor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    [CreateAssetMenu(menuName = "Dexterity/State Function", fileName = "State Function")]
    public class StateFunction : ScriptableObject, IStepList
    {
        public const int emptyStateId = -1;

        [Serializable]
        public class Step {
            public enum Type {
                Condition = 0,
                Result = 1,
                Reference = 2,
            }

            public static Step Root => new Step { id = -1 };

            public bool isRoot => id == -1;

            public int id;
            public int parent = -1;
            public Type type;
            
            // Condition
            [Field]
            public string condition_fieldName;
            [FieldValue(nameof(condition_fieldName), proxy: true)]
            public int condition_fieldValue;
            public bool condition_negate;
            [NonSerialized]
            private int condition_fieldId;
            public int GetConditionFieldID() {
                if (condition_fieldId == -1)
                    condition_fieldId = Core.instance.GetFieldID(condition_fieldName);
                return condition_fieldId;
            }

            // Result
            public string result_stateName;

            [NonSerialized]
            private int result_stateId = -1;
            public int GetResultStateID() {
                if (result_stateId == -1)
                    result_stateId = Core.instance.GetStateID(result_stateName);
                return result_stateId;
            }

            // Reference
            public StateFunction reference_stateFunction;

            public override string ToString() {
                switch (type) {
                    case Type.Condition:
                        return $"{condition_fieldName} == {condition_fieldValue}?";

                    case Type.Result:
                        return $"Go to {result_stateName}";
                }
                return base.ToString();
            }
        }

        public const string kDefaultState = "<Default>";

        private static HashSet<string> namesSet = new HashSet<string>();
        private static Stack<(Step step, int depth)> stack = new Stack<(Step step, int depth)>();

        public List<Step> steps = new List<Step>();
        private Dictionary<Step, List<Step>> cachedTree;

        List<Step> IStepList.steps => steps;

        internal int Evaluate(FieldMask mask)
        {
            if (cachedTree == null)
                cachedTree = ListToTree(steps);
            return Evaluate(cachedTree, mask);
        }

        private static int Evaluate(Dictionary<Step, List<Step>> tree, FieldMask mask) {
            var conditionMetDepth = 0;
            foreach (var (step, depth) in EnumerateTreeStepsDFS(tree)) {
                if (conditionMetDepth < depth) {
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

        public static IEnumerable<string> GetFieldNames(IStepList stepList)
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

        public static IEnumerable<string> GetStates(IStepList stepList)
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

        public bool HasFallback() => HasFallback(this);
        public static bool HasFallback(IStepList stepList) {
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

        public static IEnumerable<string> EnumerateStateNames(IEnumerable<IStepList> assets)
        {
            if (assets == null)
                yield break;

            namesSet.Clear();
            foreach (var asset in assets) {
                if (asset == null)
                    continue; 

                foreach (var state in GetStates(asset)) {
                    if (namesSet.Add(state))
                        yield return state;
                }
            }
        }
        public static IEnumerable<string> EnumerateFieldNames(IEnumerable<IStepList> assets)
        {
            if (assets == null)
                yield break;

            namesSet.Clear();
            foreach (var asset in assets) {
                if (asset == null)
                    continue; 

                foreach (var field in GetFieldNames(asset)) {
                    if (namesSet.Add(field))
                        yield return field;
                }
            }
        }

        public static IEnumerable<(Step step, int depth)> EnumerateTreeStepsDFS(IStepList stepList) {
            return EnumerateTreeStepsDFS(ListToTree(stepList.steps));
        }

        private static IEnumerable<(Step step, int depth)> EnumerateTreeStepsDFS(Dictionary<Step, List<Step>> tree) {
            stack.Clear();
            foreach (var step in tree.Keys) {
                if (step.isRoot)
                    stack.Push((step, 0));
            }

            while (stack.Count > 0) {
                var (step, depth) = stack.Pop();
                yield return (step, depth);
                if (tree.ContainsKey(step)) {
                    foreach (var child in tree[step].Reverse<Step>()) {
                        stack.Push((child, depth + 1));
                    }
                }
            }
        }


        // convert list with parent ids to tree structure where each node has a list of children
        private static Dictionary<Step, List<Step>> ListToTree(List<Step> steps) {
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

        private string PrintTreeWithDepth(Dictionary<Step, List<Step>> tree) {
            var sb = new System.Text.StringBuilder();

            foreach (var (step, depth) in EnumerateTreeStepsDFS(tree)) {
                sb.Append($"{new string('-', depth)}[{step.id}] {step.type}\n");
            }
            
            return sb.ToString();
        }
    }
}