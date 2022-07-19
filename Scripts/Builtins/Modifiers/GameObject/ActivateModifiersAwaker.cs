using UnityEngine;

// Activates dormant ActivateModifiers
//
// Problem solved: The ActivateModifier component may turn itself off during edit and saved.
//                 It then never get activated during runtime.
//
// Solution: This component searches for turned-off SAMs and wakes them up.

namespace OneHamsa.Dexterity.Visual.Builtins {
	public class ActivateModifiersAwaker : MonoBehaviour {

		void Awake() {
			foreach (var sam in GetComponentsInChildren<ActivateModifier>(includeInactive: true))
				if (!sam.gameObject.activeSelf)
					WakeUpSAM(sam);
		}

		void WakeUpSAM(ActivateModifier sam) {
			sam.gameObject.SetActive(true);
		}
	}
}