using GraphProcessor;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    public class StateFunctionGraph : BaseGraph
    {
        internal FieldsState fieldsState { get; private set; }
        internal int evaluationResult { get; set; } = -1;

        ProcessGraphProcessor processor;

        protected override void OnEnable()
        {
            base.OnEnable();
            processor = new ProcessGraphProcessor(this);
        }


        public IEnumerable<string> GetFields()
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




        public string errorString;

        public bool Validate()
        {
            // TODO
            return true;
        }
    }
}