using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Builtins {
	public class TransitionsListener : MonoBehaviour {
		[Tooltip("Optional. Will look for any Node on object or its parents")]
		[SerializeField]
		protected BaseStateNode node;

		[Range(0, 1)] 
		public float transitionProgressToConsiderDone = .99f;
		
		public List<Modifier> blacklistModifiers = new List<Modifier>();

		public bool transitioning { get; private set; }
		public event Action<int, int> onTransitionsStart;
		public event Action<int> onTransitionsEnd;

		void Awake() 
		{
			if (!node) 
			{
				node = GetComponentInParent<BaseStateNode>();
				if (!node) 
				{
					Debug.LogWarning($"Node not found for listener ({gameObject.name})");
					enabled = false;
				}
			}
		}

		void OnEnable() {
			transitioning = false;
			node.onStateChanged += OnStateChanged;
			
			var anyTransitioning = false;
			foreach (var modifier in node.GetModifiers())
			{
				modifier.onTransitionEnded += OnTransitionEnded;
				if (modifier.IsChanged())
					anyTransitioning = true;
			}
			
			if (anyTransitioning)
				OnStateChanged(StateFunction.emptyStateId, node.GetActiveState());
		}

		void OnDisable() {
			transitioning = false;
			node.onStateChanged -= OnStateChanged;
			foreach (var modifier in node.GetModifiers())
				modifier.onTransitionEnded -= OnTransitionEnded;
		}

		void OnStateChanged(int oldValue, int newValue) {
			if (transitioning)
				return;
			
			transitioning = true;
			onTransitionsStart?.Invoke(oldValue, newValue);
		}

		private void LateUpdate()
		{
			if (!transitioning)
				return;
			
			if (node.IsStateDirty())
				return; // pending state change
			
			foreach (var modifier in node.GetModifiers()) {
				if (blacklistModifiers.Contains(modifier))
					continue;
				
				if (modifier.IsChanged() && modifier.transitionProgress < transitionProgressToConsiderDone)
					return; // still not all done
			}

			// all transitions done
			transitioning = false;
			onTransitionsEnd?.Invoke(node.GetActiveState());
		}

		void OnTransitionEnded(int activeState)
		{
			LateUpdate();
		}
	}
}