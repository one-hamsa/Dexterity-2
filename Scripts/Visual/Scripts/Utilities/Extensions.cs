using System;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual {

	public static class Extensions {
		public static Component GetOrAddComponent(this GameObject obj, Type type) {
			Component comp = obj.GetComponent(type);
			if (comp == null) {
				comp = obj.AddComponent(type);
			}
			return comp;
		}
		
		public static T GetOrAddComponent<T>(this GameObject obj) where T : Component {
			return (T)GetOrAddComponent(obj, typeof(T));
		}

		public static Component GetOrAddComponent(this Component c, Type type) {
			return GetOrAddComponent(c.gameObject, type);
		}

		public static T GetOrAddComponent<T>(this Component c) where T : Component {
			return GetOrAddComponent<T>(c.gameObject);
		}
    }
}