using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using GraphProcessor;
using UnityEditor.Callbacks;
using System.IO;

namespace OneHamsa.Dexterity.Visual
{
	public class StateFunctionGraphAssetCallbacks
	{
		[MenuItem("Assets/Create/State Function", false, 10)]
		public static void CreateGraphPorcessor()
		{
			var graph = ScriptableObject.CreateInstance<StateFunctionGraph>();
			ProjectWindowUtil.CreateAsset(graph, "State Function.asset");
		}

		[OnOpenAsset(0)]
		public static bool OnBaseGraphOpened(int instanceID, int line)
		{
			var asset = EditorUtility.InstanceIDToObject(instanceID) as StateFunctionGraph;

			if (asset != null)
			{
				EditorWindow.GetWindow<StateFunctionGraphWindow>().InitializeGraph(asset);
				return true;
			}
			return false;
		}
	}
}