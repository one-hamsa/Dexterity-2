using GraphProcessor;
using System;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace OneHamsa.Dexterity.Visual
{
	[NodeCustomEditor(typeof(ConditionNode))]
	public class ConditionNodeView : BaseNodeView
	{
		const string kNoInputMessage = "No input(s), node won't run";
		const string kNotAllOutputsConnectedMessage = "Not all outputs are connected";

		ConditionNode node;

		protected override bool legacyPropertyFields => true;

        public override void Enable()
		{
			base.Enable();

			node = nodeTarget as ConditionNode;

			owner.onAfterGraphChanged += HandleGraphChanges;
			contentContainer.Q<PropertyField>(nameof(ConditionNode.fieldName))
				.RegisterValueChangeCallback(HandleFieldChange);
			HandleGraphChanges(null);
			RefreshField();

			// runtime
			node.onProcessed += HandleNodeProcessed;
			owner.initialized += HandleNodeProcessed;
		}

        public override void Disable()
		{
			base.Disable();

			owner.onAfterGraphChanged -= HandleGraphChanges;
			contentContainer.Q<PropertyField>(nameof(ConditionNode.fieldName))
				.UnregisterCallback<SerializedPropertyChangeEvent>(HandleFieldChange);

			node.onProcessed -= HandleNodeProcessed;
			owner.initialized -= HandleNodeProcessed;
		}


		private void HandleNodeProcessed()
		{
			EnableInClassList("selected", node.shouldExecute);

			if (!portsPerFieldName.TryGetValue(nameof(ConditionNode.outputs), out var ports))
				return;

			for (var i = 0; i < ports.Count; ++i)
            {
				ports[i].EnableInClassList("selected", node.processIndex == i);
			}
		}

		private void HandleGraphChanges(GraphChanges changes)
		{
			RemoveMessageView(kNotAllOutputsConnectedMessage);
			if (nodeTarget.outputPorts
				.Where(p => p.fieldName == nameof(ConditionNode.outputs))
				.Any(p => p.GetEdges().Count == 0))
				AddMessageView(kNotAllOutputsConnectedMessage, NodeMessageType.Error);


			EnableInClassList("entry", nodeTarget.computeOrder == 0);

			RemoveMessageView(kNoInputMessage);
			if (nodeTarget.computeOrder == 0)
				return;

			if (nodeTarget.inputPorts
				.FirstOrDefault(p => p.fieldName == nameof(ConditionNode.input))
				?.GetEdges()?.Count == 0)
				AddMessageView(kNoInputMessage, NodeMessageType.Warning);
		}

		private void HandleFieldChange(SerializedPropertyChangeEvent evt)
        {
			RefreshField();
		}
		
		private void RefreshField()
        {
			node.SetDefinitionFromEditor(DexteritySettingsProvider.GetFieldDefinitionByName(node.fieldName));
			owner.SaveGraphToDisk();

			UpdateTitle();
			nodeTarget.UpdateAllPorts();
			RefreshPorts();
		}
    }
}