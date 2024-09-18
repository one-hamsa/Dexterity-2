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

        
        // override HandleNodeStateChange to decouple from this gameObject's lifecycle
	    protected override void HandleNodeStateChange(int oldState, int newState)
        {
            base.HandleNodeStateChange(oldState, newState);
            var node = GetNode();
            if (this != null && node != null && node.isActiveAndEnabled && enabled)
	            // FIXME this isn't ideal. it can be called from OnEnable, which means the other components on this gameObject
				//. might never get initialized once (and skip their OnEnable/OnDisable).
				//. consider using Start() as the entry point for first-time enabling
                gameObject.SetActive(((Property)GetProperty(newState)).active);
        }

	    public override void HandleStateChange(int oldState, int newState)
	    {
		    if (!Application.IsPlaying(this))
		    {
			    // during edit time, there won't be node state changes, so handle it here
                gameObject.SetActive(((Property)GetProperty(newState)).active);
		    }
		    
		    base.HandleStateChange(oldState, newState);
	    }

	    protected override void OnDisable()
        {
            if (Manager.instance != null)
                Manager.instance.UnsubscribeFromUpdates(this);
            
            // don't cleanup node registrations
        }

        public void OnDestroy()
        {
            // cleanup now
            base.OnDisable();
        }

        public override void Refresh()
        {
	        base.Refresh();
	        // always mark as didn't change
	        transitionChanged = false;
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
	        if (!enabled)
		        return;
	        
	        bool isInstance = PrefabUtility.IsPartOfPrefabInstance(this);
	        if (!isInstance)
	        {
		        EditorUtility.SetDirty(this);
		        activeInEdit = gameObject.activeSelf;
	        }
	        
			gameObject.SetActive(true);
			EditorUtility.SetDirty(gameObject);
#endif
		}

		void PrepareForEdit() 
        {
	        if (!enabled)
		        return;
	        
#if UNITY_EDITOR
	        bool isInstance = PrefabUtility.IsPartOfPrefabInstance(this);
	        if (!isInstance)
	        {
		        gameObject.SetActive(activeInEdit);
	        }
#endif
		}
		
		#if UNITY_EDITOR
	    public override (string, LogType) GetEditorComment()
	    {
		    var node = GetNode();
		    if (GetNode() != null && node.gameObject == gameObject)
		    {
			    return ("ActivateModifier should not be used on the same GameObject as the node it is attached to.", LogType.Error);
		    }
		    return base.GetEditorComment();
	    }
#endif
    }
}
