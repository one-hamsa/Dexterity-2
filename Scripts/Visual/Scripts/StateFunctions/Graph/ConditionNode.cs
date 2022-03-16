using GraphProcessor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;

namespace OneHamsa.Dexterity.Visual
{
	[System.Serializable, NodeMenuItem("Condition", shortcut = 'c', onlyCompatibleWithGraph = typeof(StateFunctionGraph))]
	public class ConditionNode : BaseStateFunctionNode
	{
		[Output]
		public IEnumerable<bool> outputs;

		[Field(drawLabelSeparately = true), UseLegacyLabel, InspectorName("Field")]
		public string fieldName;

		public override string name => string.IsNullOrWhiteSpace(fieldName) ? "Condition" : $"{fieldName}?";

		// save definition in asset - for editing purposes
		[SerializeField, HideInInspector]
		protected FieldDefinition definition;

		[NonSerialized]
		protected int definitionId = -1;
		public int processIndex { get; private set; } = -1;

		[NonSerialized]
		private List<SerializableEdge> cachedOutputEdges;

        public override void Initialize() 
		{
			definitionId = Core.instance.GetFieldID(fieldName);
			if (definitionId == -1)
			{
				UnityEngine.Debug.LogError($"definition id == -1 (field {fieldName})");
			}
			definition = Core.instance.GetFieldDefinition(definitionId);
		}

		[Conditional("DEBUG")]
		public void SetDefinitionFromEditor(FieldDefinition fd)
        {
			definition = fd;
		}

		private void CacheOutputEdges()
        {
			cachedOutputEdges = new List<SerializableEdge>(8);
			foreach (var p in outputPorts)
			{
				if (p.fieldName == nameof(outputs))
				{
					// should only have one edge
					cachedOutputEdges.Add(p.GetEdges()[0]);
				}
}
        }

		protected override void ProcessAlways()
        {
			if (cachedOutputEdges == null)
				CacheOutputEdges();

			// reset index in case this node isn't processed
			processIndex = -1;
		}

		protected override void ProcessWhenTrue()
		{
			// find output index
			foreach (var item in stateFunction.fieldsState)
            {
				if (item.field == definitionId)
                {
					switch (definition.type)
                    {
						case Node.FieldType.Boolean when item.value == 0:
							// false is second on list
							processIndex = 1;
							break;
						case Node.FieldType.Boolean when item.value == 1:
							// true is first on list
							processIndex = 0;
							break;

						case Node.FieldType.Enum:
							processIndex = item.value;
							break;
					}
					return;
                }
            }

			UnityEngine.Debug.LogError($"didn't find anything to do for field {fieldName}");
		}

		[CustomPortBehavior(nameof(outputs))]
		IEnumerable<PortData> GetOutputPorts(List<SerializableEdge> edges)
		{
			if (definition.name == null)
				// empty definition
				yield break;

			switch (definition.type)
			{
				case Node.FieldType.Boolean:
					yield return new PortData
					{
						identifier = "true",
						displayName = "true",
						displayType = typeof(bool),
						acceptMultipleEdges = false
					};
					yield return new PortData
					{
						identifier = "false",
						displayName = "false",
						displayType = typeof(bool),
						acceptMultipleEdges = false
					};
					break;
				case Node.FieldType.Enum:
					foreach (var option in definition.enumValues)
					{
						yield return new PortData
						{
							identifier = $"enum_{option}",
							displayName = option,
							displayType = typeof(bool),
							acceptMultipleEdges = false
						};
					}
					break;
			}
		}

		[CustomPortOutput(nameof(outputs), typeof(bool), allowCast = false)]
		public void PushOutputs(List<SerializableEdge> edges)
		{
			for (var i = 0; i < cachedOutputEdges.Count; ++i)
				cachedOutputEdges[i].passThroughBuffer = processIndex == i;
		}
	}
}