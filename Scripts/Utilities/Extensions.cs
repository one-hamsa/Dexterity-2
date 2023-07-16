using System;
using UnityEngine;

namespace OneHamsa.Dexterity.Utilities {

	public static class Extensions {
		internal static Component GetOrAddComponent(this GameObject obj, Type type) {
			Component comp = obj.GetComponent(type);
			if (comp == null) {
				comp = obj.AddComponent(type);
			}
			return comp;
		}
		
		internal static T GetOrAddComponent<T>(this GameObject obj) where T : Component {
			return (T)GetOrAddComponent(obj, typeof(T));
		}

		internal static Component GetOrAddComponent(this Component c, Type type) {
			return GetOrAddComponent(c.gameObject, type);
		}

		internal static T GetOrAddComponent<T>(this Component c) where T : Component {
			return GetOrAddComponent<T>(c.gameObject);
		}
        
        internal static string GetPath(this GameObject go)
        {
            string name = go.name;
            while (go.transform.parent != null)
            {

                go = go.transform.parent.gameObject;
                name = go.name + "/" + name;
            }
            return name;
        }
    }
}