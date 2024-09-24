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
        
		public struct RaycastResult
		{
			public enum Result
			{
				Default,
				CanAccept,
				CannotAccept,
				Accepted,
			}
			
			public Result result;
			public IRaycastReceiver hitReceiver;
		}
    }

    public struct DexterityRaycastHit
    {
	    public int priority;
	    public float distance;
	    public Vector3 point;
	    public Vector3 normal;
	    public Collider collider;
	    public Transform transform;
    }
}
