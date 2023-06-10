using UnityEngine;

namespace OneHamsa.Dexterity.Visual
{
    public interface IRaycastController
    {
        bool CompareTag(string tag);
        bool isPressed { get; }
        bool wasPressedThisFrame { get; }
        Vector3 position { get; }
        Vector3 forward { get; }
        
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
