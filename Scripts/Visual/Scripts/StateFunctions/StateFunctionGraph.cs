using GraphProcessor;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    public class StateFunctionGraph : BaseGraph
    {
        public const string kDefaultState = "<Default>";

        public bool initialized { get; private set; }
        internal FieldsState fieldsState { get; private set; }
        internal int evaluationResult { get; set; } = -1;

        ProcessGraphProcessor processor;

        protected override void OnEnable()
        {
            base.OnEnable();
            processor = new ProcessGraphProcessor(this);
        }

        /// <summary>
        /// Initializes runtime data for nodes (IDs etc.)
        /// </summary>
        public void Initialize() {
            if (initialized) {
                Debug.LogWarning($"State Function {name} already initialized", this);
                return;
            }
            foreach (var node in nodes)
            {
                if (!(node is BaseStateFunctionNode nodeBase))
                    continue;

                nodeBase.Initialize();
            }
            initialized = true;
        }

        public IEnumerable<string> GetFieldNames()
        {
            foreach (var node in nodes)
            {
                if (node is ConditionNode cond)
                    yield return cond.fieldName;
            }
        }

        internal int Evaluate(FieldsState fieldsState)
        {
            // assign for graph
            this.fieldsState = fieldsState;
            // run graph
            evaluationResult = -1;
            processor.Run();
            // assume someone changed result
            return evaluationResult;
        }

        public IEnumerable<string> GetStates()
        {
            // all state functions have a default state
            yield return kDefaultState;

            foreach (var node in nodes)
            {
                if (node is DecisionNode desc) {
                    if (desc.fallthrough)
                        continue;
                    
                    yield return desc.stateName;
                }
            }
        }

        public string errorString;

        public bool Validate()
        {
            // TODO
            return true;
        }

        private static HashSet<string> namesSet = new HashSet<string>();

        public static IEnumerable<string> EnumerateStateNames(IEnumerable<StateFunctionGraph> assets)
        {
            if (assets == null)
                yield break;

            namesSet.Clear();
            foreach (var asset in assets) {
                if (asset == null)
                    continue; 

                foreach (var state in asset.GetStates()) {
                    if (namesSet.Add(state))
                        yield return state;
                }
            }
        }
        public static IEnumerable<string> EnumerateFieldNames(IEnumerable<StateFunctionGraph> assets)
        {
            if (assets == null)
                yield break;

            namesSet.Clear();
            foreach (var asset in assets) {
                if (asset == null)
                    continue; 

                foreach (var field in asset.GetFieldNames()) {
                    if (namesSet.Add(field))
                        yield return field;
                }
            }
        }
    }
}