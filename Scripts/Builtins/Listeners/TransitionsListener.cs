using System;
using UnityEngine;

namespace OneHamsa.Dexterity.Builtins {
	[RequireComponent(typeof(FieldNode))]
	public class TransitionsListener : MonoBehaviour {
		[Tooltip("Optional. Will look for any Node on object or its parents")]
		[SerializeField]
		protected FieldNode node;

		[Range(0, 1)] 
		public float transitionProgressToConsiderDone = .99f;

		public bool transitioning { get; private set; }
		public event Action<int, int> onTransitionsStart;
		public event Action<int> onTransitionsEnd;

		void Awake() {
			if (!node) {
				node = GetComponentInParent<FieldNode>();
				if (!node) {
					Debug.LogWarning($"Node not found for listener ({gameObject.name})");
					enabled = false;
				}
			}
		}

		void OnEnable() {
			transitioning = false;
			node.onStateChanged += OnStateChanged;
			
			var anyTransitioning = false;
			foreach (var modifier in Modifier.GetModifiers(node))
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
			foreach (var modifier in Modifier.GetModifiers(node))
				modifier.onTransitionEnded -= OnTransitionEnded;
		}

		void OnStateChanged(int oldValue, int newValue) {
			if (transitioning)
				return;
			
			transitioning = true;
			onTransitionsStart?.Invoke(oldValue, newValue);
		}

		private void Update()
		{
			if (!transitioning)
				return;
			
			foreach (var modifier in Modifier.GetModifiers(node)) {
				if (modifier.IsChanged() && modifier.transitionProgress < transitionProgressToConsiderDone)
					return; // still not all done
			}

			// all transitions done
			transitioning = false;
			onTransitionsEnd?.Invoke(node.GetActiveState());
		}

		void OnTransitionEnded(int activeState)
		{
			Update();
		}
	}
}