using UnityEngine;
using System;
using OneHamsa.Dexterity.Utilities;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace OneHamsa.Dexterity.Builtins
{
    public class ActivateModifier : Modifier, ISceneEditingHooks, IPrefabEditingHooks
    {
        public override bool animatableInEditor => enabled;
        [SerializeField] [HideInInspector] bool activeInEdit;

        [Serializable]
        public class Property : PropertyBase
        {
            // custom params
            public bool active;
        }

        public override void HandleStateChange(int oldState, int newState)
        {
            base.HandleStateChange(oldState, newState);
            var node = GetNode();
            if (this != null && node != null && node.isActiveAndEnabled && enabled)
                gameObject.SetActive(((Property)GetProperty(newState)).active);
        }

        protected override void OnDisable()
        {
            // actually, don't cleanup just yet. we rely on coroutine
        }

        public void OnDestroy()
        {
            // cleanup now
            base.OnDisable();
        }
        
		void ISceneEditingHooks.OnSceneSaving(bool duringBuild) { if (!duringBuild) PrepareForSave(); }
		void ISceneEditingHooks.OnSceneSaved(bool duringBuild) { if (!duringBuild) PrepareForEdit(); }
		void ISceneEditingHooks.OnSceneOpened(bool duringBuild) { if (!duringBuild) PrepareForEdit(); }

		void IPrefabEditingHooks.OnPrefabSaving() { PrepareForSave(); }
		void IPrefabEditingHooks.OnPrefabSaved() { PrepareForEdit(); }
		void IPrefabEditingHooks.OnPrefabStageOpened() { PrepareForEdit(); }

		void PrepareForSave() 
        {
#if UNITY_EDITOR
	        activeInEdit = gameObject.activeSelf;
	        EditorUtility.SetDirty(this);
			gameObject.SetActive(true);
			EditorUtility.SetDirty(gameObject);
#endif
		}

		void PrepareForEdit() 
        {
#if UNITY_EDITOR
			gameObject.SetActive(activeInEdit);
#endif
		}
    }
}
