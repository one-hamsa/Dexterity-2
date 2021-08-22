using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using GraphProcessor;

namespace OneHamsa.Dexterity.Visual
{
	public class StateFunctionGraphWindow : BaseGraphWindow
	{
        protected override void OnDestroy()
		{
			graphView?.Dispose();
		}

		protected override void InitializeWindow(BaseGraph graph)
		{
			if (!(graph is StateFunctionGraph))
			{
				Debug.LogWarning("graph is not StateFunctionGraph, not opening");
				Close();
				return;
			}

			titleContent = new GUIContent("State Function Graph");

			if (graphView == null)
			{
				graphView = new StateFunctionGraphView(this);

				var toolbar = new StateFunctionGraphToolbarView(graphView);
				graphView.Add(toolbar);
			}

			rootView.Add(graphView);
		}
	}

}