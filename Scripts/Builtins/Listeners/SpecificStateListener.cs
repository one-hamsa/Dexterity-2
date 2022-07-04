using OneHamsa.Dexterity.Visual;
using UnityEngine;
using UnityEngine.Events;

namespace OneHamsa.Dexterity.Visual.Builtins {
	public class SpecificStateListener : MonoBehaviour, IReferencesNode {

		[Tooltip("Optional. Will look for any Node on object or it parents")]
		[SerializeField]
		protected Node node;

		DexterityBaseNode IReferencesNode.node => GetComponentInParent<DexterityBaseNode>();

		[State]
		public string stateName;

		public UnityEvent OnEnterState;
		public UnityEvent OnExitState;

		int _stateID;

		void Awake() {
			if (!node)
				node = GetComponentInParent<Node>();

			if (!node) {
				Debug.LogError($"Node not found on ({gameObject.name})", this);
				enabled = false;
			}
		}

		void OnEnable() {
			if (!node) return;
			_stateID = Core.instance.GetStateID(stateName);
			_inTheState = node.activeState == _stateID;
			node.onStateChanged += OnStateChanged;
		}

		void OnDisable() {
			if (!node) return;
			node.onStateChanged -= OnStateChanged;
		}

		bool _inTheState = false;

		bool inTheState {
			set {
				if (_inTheState != value) {
					_inTheState = value;
					(_inTheState ? OnEnterState : OnExitState)?.Invoke();
				}
			}
		}

		void OnStateChanged(int oldValue, int newValue) {
			inTheState = newValue == _stateID;
		}
	}
}