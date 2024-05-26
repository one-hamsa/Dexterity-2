using OneHamsa.Dexterity.Builtins;
using UnityEngine;

namespace OneHamsa.Dexterity
{
    public interface IRaycastController
    {
        bool CompareTag(string tag);
        bool isPressed { get; }
        bool wasPressedThisFrame { get; }
        Vector3 position { get; }
        Vector3 forward { get; }
        Vector3 up { get; }
        DexterityRaycastHit hit { get; }

        Vector2 scroll { get; }

        public bool showVisibleRay { get; }

        public Quaternion rotation => Quaternion.LookRotation(forward, up);
        
		public ref struct RaycastEvent
		{
			public enum Result
			{
				Default,
				CanAccept,
				CannotAccept,
				Accepted,
			}
			
			public DexterityRaycastHit hit;
			public Result result;
		}
    }

    public struct DexterityRaycastHit
    {
	    public int priority;
	    public float distance;
	    public Vector3 point;
	    public Collider collider;
	    public Transform transform;
    }
}
