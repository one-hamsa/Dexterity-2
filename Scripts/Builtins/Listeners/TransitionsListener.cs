using System;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual.Builtins {
	[RequireComponent(typeof(Node))]
	public class TransitionsListener : MonoBehaviour {
		[Tooltip("Optional. Will look for any Node on object or its parents")]
		[SerializeField]
		protected Node node;

		public bool transitioning { get; private set; }
		public event Action<int> onTransitionsEnd;

		void Awake() {
			if (!node) {
				node = GetComponentInParent<Node>();
				if (!node) {
					Debug.LogWarning($"Node not found for listener ({gameObject.name})");
					enabled = false;
				}
			}
		}

		void OnEnable() {
			transitioning = false;
			node.onStateChanged += OnStateChanged;
			foreach (var modifier in Modifier.GetModifiers(node))
				modifier.onTransitionEnded += OnTransitionEnded;
		}

		void OnDisable() {
			transitioning = false;
			node.onStateChanged -= OnStateChanged;
			foreach (var modifier in Modifier.GetModifiers(node))
				modifier.onTransitionEnded -= OnTransitionEnded;
		}

		void OnStateChanged(int oldValue, int newValue) {
			transitioning = true;
		}

		void OnTransitionEnded(int activeState) {
			foreach (var modifier in Modifier.GetModifiers(node)) {
				if (modifier.IsChanged())
					return; // still not all done
			}

			// all transitions done
			transitioning = false;
			onTransitionsEnd?.Invoke(activeState);
		}
	}
}