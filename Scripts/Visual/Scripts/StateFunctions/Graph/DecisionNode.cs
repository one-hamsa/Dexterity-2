using GraphProcessor;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OneHamsa.Dexterity.Visual
{
	[System.Serializable, NodeMenuItem("Decision", shortcut = 'd', onlyCompatibleWithGraph = typeof(StateFunctionGraph))]
	public class DecisionNode : BaseStateFunctionNode
	{
		[InspectorName("State")]
		public string stateName;

		public override string name => string.IsNullOrWhiteSpace(stateName) ? "Decision" : $"= {stateName}";

		private int stateId = -1;

        public override void Initialize()
		{ 
			stateId = Manager.instance.GetStateID(stateName);
			if (stateId == -1)
				Debug.LogError($"state id == -1 (state {stateName})"); 
		}

		protected override void ProcessWhenTrue()
		{ 
			stateFunction.evaluationResult = stateId;
		}
	}
}