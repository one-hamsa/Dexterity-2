using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class RaycastController : MonoBehaviour, IRaycastController
	{
		const float rayLength = 100f;
		const int maxHits = 20;

		public LayerMask layerMask = int.MaxValue;
		public InputAction pressed;

		public RaycastController[] otherControllers;
		public bool defaultController;


		[Header("Debug")]
		public Color debugColliderColor = new Color(1f, .5f, 0f);
		public Color debugHitColor = new Color(1f, .25f, 0f);

		public bool current => enabled && ((lastControllerPressed == null && defaultController) || lastControllerPressed == this);

		public Ray ray { get; private set; }
		public bool didHit { get; private set; }
		public RaycastHit hit { get; private set; }
		public bool isLocked => lockedOn != null;
		private IRaycastReceiver lockedOn;
		private int pressStartFrame = -1;
		private RaycastController lastControllerPressed;

		RaycastHit[] hits = new RaycastHit[maxHits];
		List<IRaycastReceiver> lastReceivers = new List<IRaycastReceiver>(4), 
		potentialReceiversA = new List<IRaycastReceiver>(4), 
		potentialReceiversB = new List<IRaycastReceiver>(4);

		Dictionary<IRaycastReceiver, long> _recentlyHitReceivers = new();

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
			pressStartFrame = Time.frameCount;
			
			lastControllerPressed = this;
			foreach (var other in otherControllers)
			{
				other.HandleOtherPressed(this);
			}
		}

		private void HandleOtherPressed(RaycastController controller) 
		{
			lastControllerPressed = controller;
			foreach (var receiver in lastReceivers)
            {
				receiver?.ClearHit(this);
			}
			lastReceivers.Clear();
		}

        void Update()
        {
			long now = Stopwatch.GetTimestamp();

			didHit = false;

			if (!current)
				return;

			Vector3 origin = transform.position;
			Vector3 direction = transform.forward;

			ray = new Ray(origin, direction);
			Debug.DrawRay(ray.origin, ray.direction * rayLength, Color.blue);
					
			int numHits = Physics.RaycastNonAlloc(ray, hits, rayLength, layerMask);

			float minDist = float.PositiveInfinity;
			hit = new RaycastHit();
			List<IRaycastReceiver> closestReceivers = null;

			var isRaycastReceiverIncluded = Core.instance.isRaycastReceiverIncluded;
			for (int i = 0; i < numHits; ++i)
			{
				if (isRaycastReceiverIncluded != null && !isRaycastReceiverIncluded(hits[i].collider))
					continue;

				hits[i].collider.gameObject.GetComponents(potentialReceiversA);
				if (potentialReceiversA.Count != 0 && hits[i].distance < minDist)
				{
					// save hit
					hit = hits[i];
					minDist = hits[i].distance;
					closestReceivers = potentialReceiversA;
					// swap pointers
					(potentialReceiversA, potentialReceiversB) = (potentialReceiversB, potentialReceiversA);
				}
			}

			foreach (var receiver in lastReceivers)
            {
	            if (
		            // no new receivers
		            closestReceivers == null
		            // this receiver is no longer relevant
		            || !closestReceivers.Contains(receiver)
		            // this controller is locked, but not by this receiver
		            || (isLocked && lockedOn != receiver))
	            {
					_recentlyHitReceivers[receiver] = now;
		            receiver?.ClearHit(this);
	            }
            }
			lastReceivers.Clear();

			if (closestReceivers != null)
            {
				didHit = true;
				foreach (var receiver in closestReceivers)
				{
					if (isLocked && lockedOn != receiver)
						// this controller is locked, but not by this receiver
						continue;

					// repeat-hit cooldown
					if (_recentlyHitReceivers.ContainsKey(receiver)) {
						float cooldown = (float)(now - _recentlyHitReceivers[receiver]) / Stopwatch.Frequency;
						if (cooldown < Core.instance.settings.repeatHitCooldown)
							continue;
					}

					receiver.ReceiveHit(this, hit);
					lastReceivers.Add(receiver);
				}
			}
		}

		private void OnDrawGizmos() {
			if (!didHit)
				return;

			Gizmos.color = debugHitColor;
			Gizmos.DrawWireSphere(hit.point, .025f);

			if (hit.collider == null)
				return;

			Gizmos.matrix = hit.collider.transform.localToWorldMatrix;
			Gizmos.color = debugColliderColor;
			DrawCollider(hit.collider);
		}

        private void DrawCollider(Collider collider)
        {
            if (collider is BoxCollider box)
				Gizmos.DrawWireCube(box.center, box.size);
			else if (collider is SphereCollider sphere)
				Gizmos.DrawWireSphere(sphere.center, sphere.radius);

			// TODO more, maybe with collider.bounds, just need to understand in what space
        }

		public bool Lock(IRaycastReceiver receiver) 
		{
			if (lockedOn != null)
				return false;

			lockedOn = receiver;
			return true;
		}
		public bool Unlock(IRaycastReceiver receiver)
		{
			if (lockedOn != receiver)
				return false;

			lockedOn = null;
			return true;
		}

		string IRaycastController.tag => tag;
        bool IRaycastController.isPressed => (pressed.phase == InputActionPhase.Started);
		bool IRaycastController.wasPressedThisFrame => (pressStartFrame == Time.frameCount);
		Vector3 IRaycastController.position => transform.position;
		Vector3 IRaycastController.forward => transform.forward;
    }
}
