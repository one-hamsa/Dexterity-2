using UnityEditor;
using GraphProcessor;
using UnityEngine.UIElements;

namespace OneHamsa.Dexterity.Visual
{
	[CustomEditor(typeof(StateFunctionGraph), true)]
	public class StateFunctionGraphInspector : GraphInspector
	{
		protected override void CreateInspector()
		{
			//base.CreateInspector();

			root.Add(new Button(() => EditorWindow
				.GetWindow<StateFunctionGraphWindow>().InitializeGraph(target as BaseGraph))
			{
				text = "Open graph window"
			});
		}
	}
}