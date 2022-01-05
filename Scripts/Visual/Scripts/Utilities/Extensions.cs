using UnityEngine;

namespace OneHamsa.Dexterity.Visual {

	public static class Extensions {

		public static T GetOrAddComponent<T>(this GameObject obj) where T : Component {
			T comp = obj.GetComponent<T>();
			if (comp == null) {
				comp = obj.AddComponent<T>();
			}
			return comp;
		}

		public static T GetOrAddComponent<T>(this Component c) where T : Component {
			return GetOrAddComponent<T>(c.gameObject);
		}
    }
}