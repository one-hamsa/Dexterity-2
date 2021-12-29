using GraphProcessor;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OneHamsa.Dexterity.Visual
{
	[System.Serializable]
	public abstract class BaseStateFunctionNode : BaseNode
	{
		[Input(name = "In", allowMultiple = true)]
		public bool input;

		protected StateFunctionGraph stateFunction { get; private set; }
		public bool shouldExecute { get; private set; }

        protected sealed override void Enable()
		{
			base.Enable();

			stateFunction = graph as StateFunctionGraph;
			if (stateFunction == null)
			{
				Debug.LogError($"graph is not state function");
			}

			if (stateFunction.isRuntime)
				EnableRuntime();
			else
				EnableEditor();
		}

		protected sealed override void Process()
		{
			ProcessAlways();
			if (shouldExecute)
            {
				ProcessWhenTrue();
            }
		}

		protected virtual void EnableRuntime() { }
		protected virtual void EnableEditor() { }
		 
		protected virtual void ProcessAlways() { }
		protected abstract void ProcessWhenTrue();

		[CustomPortBehavior(nameof(input))]
		protected IEnumerable<PortData> GetInputPort(List<SerializableEdge> edges)
		{
			yield return new PortData
			{
				identifier = "in",
				displayName = "In",
				displayType = typeof(bool),
				acceptMultipleEdges = true
			};
		}
		 
		[CustomPortInput(nameof(input), typeof(bool), allowCast = false)]
		protected void PullInput(List<SerializableEdge> edges)
		{
			if (computeOrder == 0)
            {
				// we're first, execute always
				shouldExecute = true;
				return;
            }

			foreach (var edge in edges)
            {
				if (edge.passThroughBuffer != null && (bool)(edge.passThroughBuffer))
                {
					shouldExecute = true;
					return;
				}
            }
			shouldExecute = false;
		}
	}
}