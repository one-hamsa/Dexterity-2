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
        RaycastHit hit { get; }

        Vector2 scroll { get; }

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
			
			public RaycastHit hit;
			public Result result;
		}
    }
}
