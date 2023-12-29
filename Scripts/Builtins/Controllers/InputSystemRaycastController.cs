using UnityEngine;
using UnityEngine.InputSystem;

namespace OneHamsa.Dexterity.Builtins
{
    public class InputSystemRaycastController : RaycastController
	{
		public InputAction pressed;
		public override bool isPressed => pressed.phase == InputActionPhase.Started;

		private void OnEnable()
        {
			pressed.Enable();			
			pressed.performed += HandlePressed;
		}
		private void OnDisable()
        {
			pressed.Disable();
			pressed.performed -= HandlePressed;
		}

		private void HandlePressed(InputAction.CallbackContext context)
		{
			HandlePressed();
		}
    }
}
