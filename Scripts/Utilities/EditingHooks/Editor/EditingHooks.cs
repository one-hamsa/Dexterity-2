using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace OneHamsa.Dexterity.Utilities {
	[InitializeOnLoad]
	static class EditingHooks 
	{
		static EditingHooks() 
		{
			PrefabStage.prefabStageOpened -= OnPrefabStageOpened;
			PrefabStage.prefabStageOpened += OnPrefabStageOpened;
			PrefabStage.prefabStageClosing -= OnPrefabStageClosing;
			PrefabStage.prefabStageClosing += OnPrefabStageClosing;
			PrefabStage.prefabSaving -= OnPrefabSaving;
			PrefabStage.prefabSaving += OnPrefabSaving;
			PrefabStage.prefabSaved -= OnPrefabSaved;
			PrefabStage.prefabSaved += OnPrefabSaved;

			EditorSceneManager.sceneOpened -= OnSceneOpened;
			EditorSceneManager.sceneOpened += OnSceneOpened;
			EditorSceneManager.sceneClosing -= OnSceneClosing;
			EditorSceneManager.sceneClosing += OnSceneClosing;
			EditorSceneManager.sceneSaving -= OnSceneSaving;
			EditorSceneManager.sceneSaving += OnSceneSaving;
			EditorSceneManager.sceneSaved -= OnSceneSaved;
			EditorSceneManager.sceneSaved += OnSceneSaved;
			
			EditorApplication.playModeStateChanged -= OnPlayModeStateChanged; 
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

			EditorApplication.delayCall -= ForceRefresh;
			EditorApplication.delayCall += ForceRefresh;
		}

		private static void ForceRefresh()
		{
			EditorApplication.delayCall -= ForceRefresh;
			
			if (EditorApplication.isPlayingOrWillChangePlaymode) 
				return;
			
			if (PrefabStageUtility.GetCurrentPrefabStage() != null) {
				OnPrefabStageOpened(PrefabStageUtility.GetCurrentPrefabStage());
			}

			if (SceneManager.loadedSceneCount > 0) {
				for (int i = 0; i < SceneManager.loadedSceneCount; i++) {
					var scene = SceneManager.GetSceneAt(i);
					OnSceneOpened(scene, OpenSceneMode.Additive);
				}
			}
		}

		private static void OnPlayModeStateChanged(PlayModeStateChange state)
		{
			if (state == PlayModeStateChange.ExitingEditMode)
			{
				for (int i = 0; i < SceneManager.sceneCount; i++)
				{
					var scene = SceneManager.GetSceneAt(i);
					OnSceneSaving(scene, scene.path);
				}
			}
			if (state == PlayModeStateChange.EnteredEditMode)
			{
				for (int i = 0; i < SceneManager.sceneCount; i++)
				{
					var scene = SceneManager.GetSceneAt(i);
					OnSceneOpened(scene, OpenSceneMode.Additive);
				}
			}
		}

		private static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode) {
			var roots = scene.GetRootGameObjects();
			foreach (var root in roots) {
				ISceneEditingHooks[] hooks = root.GetComponentsInChildren<ISceneEditingHooks>(true);
				foreach (ISceneEditingHooks hook in hooks) {
					if (hook as Component == null) continue; // in case it got destroyed by one of the other hooks
					hook.OnSceneOpened(BuildPipeline.isBuildingPlayer);
				}
			}
		}

		private static void OnSceneClosing(UnityEngine.SceneManagement.Scene scene, bool removingScene) {
			if (!scene.isLoaded) return;
			var roots = scene.GetRootGameObjects();
			foreach (var root in roots) {
				ISceneEditingHooks[] hooks = root.GetComponentsInChildren<ISceneEditingHooks>(true);
				foreach (ISceneEditingHooks hook in hooks) {
					if (hook as Component == null) continue; // in case it got destroyed by one of the other hooks
					hook.OnSceneClosing(BuildPipeline.isBuildingPlayer);
				}
			}
		}

		private static void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path) {
			var roots = scene.GetRootGameObjects();
			foreach (var root in roots) {
				ISceneEditingHooks[] hooks = root.GetComponentsInChildren<ISceneEditingHooks>(true);
				foreach (ISceneEditingHooks hook in hooks) {
					if (hook as Component == null) continue; // in case it got destroyed by one of the other hooks
					hook.OnSceneSaving(BuildPipeline.isBuildingPlayer);
				}
			}
		}

		private static void OnSceneSaved(UnityEngine.SceneManagement.Scene scene) {
			var roots = scene.GetRootGameObjects();
			foreach (var root in roots) {
				ISceneEditingHooks[] hooks = root.GetComponentsInChildren<ISceneEditingHooks>(true);
				foreach (ISceneEditingHooks hook in hooks) {
					if (hook as Component == null) continue; // in case it got destroyed by one of the other hooks
					hook.OnSceneSaved(BuildPipeline.isBuildingPlayer);
				}
			}
		}

		private static void OnPrefabSaved(GameObject obj) {
			IPrefabEditingHooks[] hooks = obj.GetComponentsInChildren<IPrefabEditingHooks>(true);
			foreach (IPrefabEditingHooks hook in hooks) {
				if (hook as Component == null) continue; // in case it got destroyed by one of the other hooks
				hook.OnPrefabSaved();
			}
		}

		private static void OnPrefabStageOpened(PrefabStage stage) {
			IPrefabEditingHooks[] hooks = stage.prefabContentsRoot.GetComponentsInChildren<IPrefabEditingHooks>(true);
			foreach (IPrefabEditingHooks hook in hooks) {
				if (hook as Component == null) continue; // in case it got destroyed by one of the other hooks
				hook.OnPrefabStageOpened();
			}
		}

		private static void OnPrefabSaving(GameObject obj) {
			IPrefabEditingHooks[] hooks = obj.GetComponentsInChildren<IPrefabEditingHooks>(true);
			foreach (IPrefabEditingHooks hook in hooks) {
				if (hook as Component == null) continue; // in case it got destroyed by one of the other hooks
				hook.OnPrefabSaving();
			}
		}

		private static void OnPrefabStageClosing(PrefabStage stage) {
			var hooks = stage.prefabContentsRoot.GetComponentsInChildren<IPrefabEditingHooks>(true);
			foreach (IPrefabEditingHooks hook in hooks) {
				if (hook as Component == null) continue; // in case it got destroyed by one of the other hooks
				hook.OnPrefabStageClosing();
			}
		}
	}
}
