using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;

namespace OneHamsa.Dexterity
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
            
            public bool initialized => type == Type.Reference 
                                       || (type == Type.Condition && condition_fieldId != -1) 
                                       || (type == Type.Result && result_stateId != -1);
            
            // Condition
            [Field]
            public string condition_fieldName;
            public bool condition_fieldValue;
            public bool condition_negate;
            [NonSerialized]
            private int condition_fieldId = -1;
            public int GetConditionFieldID() {
                if (condition_fieldId == -1)
                    Debug.LogError($"{nameof(GetConditionFieldID)}: {condition_fieldName}: id not initialized");
                return condition_fieldId;
            }

            // Result
            public string result_stateName;

            [NonSerialized]
            private int result_stateId = -1;
            public int GetResultStateID() {
                if (result_stateId == -1)
                    Debug.LogError($"{nameof(GetResultStateID)}: {result_stateName}: id not initialized");
                return result_stateId;
            }

            // Reference
            public StateFunction reference_stateFunction;

            public override string ToString() {
                switch (type) {
                    case Type.Condition:
                        return $"{condition_fieldName} {(condition_negate ? "!=" : "==")} {condition_fieldValue}?";

                    case Type.Result:
                        return $"Go to {result_stateName}";

                    case Type.Reference:
                        return $"Run {reference_stateFunction.name}";
                }
                return base.ToString();
            }

            internal void Initialize()
            {
                switch (type) {
                    case Type.Condition:
                        condition_fieldId = Database.instance.GetStateID(condition_fieldName);
                        break;
                    case Type.Result:
                        result_stateId = Database.instance.GetStateID(result_stateName);
                        break;
                    case Type.Reference:
                        if (reference_stateFunction == null)
                            break;
                        foreach (var step in reference_stateFunction.steps) {
                            step.Initialize();
                        }
                        break;
                }
            }
        }

        public const string kDefaultState = "<Default>";

        private static HashSet<string> namesSet = new HashSet<string>();
        private static Stack<(Step step, int depth)> stack = new Stack<(Step step, int depth)>();

        public List<Step> steps = new List<Step>();
        private List<(Step step, int depth)> stepEvalCache;
        private int lastEvaluationResult = emptyStateId;
        private HashSet<string> stateNames;
        private HashSet<string> fieldNames;
        public int GetLastEvaluationResult() => lastEvaluationResult;

        List<Step> IStepList.steps => steps;

        internal int Evaluate(List<(int field, bool mask)> mask)
        {
            List<(Step step, int depth)> usedStepCahce;
            if (Application.IsPlaying(this))
            {
                if (stepEvalCache == null)
                    stepEvalCache = ListPool<(Step step, int depth)>.Get();

                usedStepCahce = stepEvalCache;
            }
            else
            {
                usedStepCahce = new();
            }
            
            (this as IStepList).PopulateStepCache(usedStepCahce);
            
            var result = IStepList.Evaluate(usedStepCahce, mask);
            lastEvaluationResult = result;
            return result;
        }

        public static IEnumerable<string> EnumerateStateNames(IEnumerable<IStepList> assets)
        {
            if (assets == null)
                yield break;

            namesSet.Clear();
            foreach (var asset in assets) {
                if (asset == null)
                    continue; 

                foreach (var state in asset.GetStateNames()) {
                    if (namesSet.Add(state))
                        yield return state;
                }
            }
        }

        HashSet<string> IHasStates.GetStateNames()
        {
            stateNames ??= (this as IStepList).GetStepListStateNames().ToHashSet();
            return stateNames;
        }
    }
}