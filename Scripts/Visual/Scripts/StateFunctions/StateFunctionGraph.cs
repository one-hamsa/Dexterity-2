using GraphProcessor;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    public class StateFunctionGraph : BaseGraph
    {
        private static StateFunctionGraph lastPrefab;
        private static Dictionary<StateFunctionGraph, StateFunctionGraph> prefabToRuntime
            = new Dictionary<StateFunctionGraph, StateFunctionGraph>();

        public bool isRuntime => source != null;
        public StateFunctionGraph source { get; private set; }
        internal FieldsState fieldsState { get; private set; }
        internal int evaluationResult { get; set; } = -1;

        ProcessGraphProcessor processor;

        protected override void OnEnable()
        {
            // HACK: save prefab
            //. (see https://forum.unity.com/threads/prefab-with-reference-to-itself.412240/)
            source = lastPrefab;
            lastPrefab = null;

            if (isRuntime)
                Manager.instance.RegisterStateFunction(this);

            base.OnEnable();
            processor = new ProcessGraphProcessor(this);
        }


        public IEnumerable<int> GetFieldIDs()
        {
            foreach (var name in GetFieldNames())
            {
                yield return Manager.instance.GetFieldID(name);
            }
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
            foreach (var node in nodes)
            {
                if (node is DecisionNode desc)
                    yield return desc.stateName;
            }
        }
        public IEnumerable<int> GetStateIDs()
        {
            foreach (var stateName in GetStates())
                yield return Manager.instance.GetStateID(stateName);
        }

        public StateFunctionGraph GetRuntimeInstance()
        {
            if (isRuntime)
            {
                Debug.LogWarning("asking for runtime but we're already a runtime instance", this);
                return this;
            }

            if (!prefabToRuntime.TryGetValue(this, out var runtime))
            {
                lastPrefab = this;
                prefabToRuntime[this] = runtime = Instantiate(this);
            }

            return runtime;
        }

        private void OnDestroy()
        {
            if (isRuntime)
                prefabToRuntime.Remove(this);
        }

        public string errorString;

        public bool Validate()
        {
            // TODO
            return true;
        }
    }
}