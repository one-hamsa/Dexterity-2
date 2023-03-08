using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class RaycastController : MonoBehaviour, IRaycastController
	{
		public class PressAnywhereEvent
		{
			public bool propagate { get; private set; } = true;
			public void StopPropagation() { propagate = false; }
		}
		
		const float rayLength = 100f;
		const int maxHits = 20;

        // Raycast Receiver filter
        private static List<RaycastFilter> isRaycastReceiverIncluded = new();
        public static event Action<PressAnywhereEvent> onAnyPress;

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
		List<IRaycastReceiver> lastReceivers = new(4), 
			potentialReceiversA = new(4), 
			potentialReceiversB = new(4),
			receiversBeforeFilter = new(1);

		Dictionary<IRaycastReceiver, long> _recentlyHitReceivers = new();
		
		public delegate bool RaycastFilter(IRaycastReceiver receiver);
        /// <summary>
        /// Set Global Raycast Receiver filter
        /// </summary>
        public static RaycastFilter AddFilter(RaycastFilter filter) {
            isRaycastReceiverIncluded.Add(filter);
            return filter;
        }

        public static RaycastFilter AddFilter(Transform root, RaycastFilter orFilter = null)
        {
			// cache all transforms
			var childReceivers = root.GetComponentsInChildren<IRaycastReceiver>(true).ToHashSet();
			bool Filter(IRaycastReceiver r)
			{
				if (childReceivers.Contains(r) || (orFilter != null && orFilter(r)))
					return true;
				
				var t = ((Component)r).transform;
				while (t != null)
				{
					if (t == root)
						return true;
					t = t.parent;
				}

				return false;
			}

			AddFilter(Filter);
			return Filter;
        }
        public static RaycastFilter AddBlockingFilter(Transform root)
        {
			// cache all transforms
			var childReceivers = root.GetComponentsInChildren<IRaycastReceiver>(true).ToHashSet();
			bool Filter(IRaycastReceiver r)
			{
				if (childReceivers.Contains(r))
					return false;
				
				var t = ((Component)r).transform;
				while (t != null)
				{
					if (t == root)
						return false;
					t = t.parent;
				}

				return true;
			}

			AddFilter(Filter);
			return Filter;
        }

        public static void RemoveFilter(RaycastFilter filter)
        {
	        if (!isRaycastReceiverIncluded.Remove(filter))
		        Debug.LogWarning($"trying to remove a filter that is no longer registered: {filter}");
        }
        
        public static void ClearFilters()
		{
			if (isRaycastReceiverIncluded.Count > 0)
			{
				// log this, because if we got to clearing filters it means someone didn't clean up after themself...!
				Debug.LogWarning($"clearing {isRaycastReceiverIncluded.Count} filters");
				isRaycastReceiverIncluded.Clear();
			}
		}
        

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
			lastControllerPressed = this;
			foreach (var other in otherControllers)
			{
				other.HandleOtherPressed(this);
			}
			
			if (onAnyPress != null)
			{
				var args = new PressAnywhereEvent();
				onAnyPress.Invoke(args);
				if (!args.propagate)
					return;
			}

			pressStartFrame = Time.frameCount;
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

			for (int i = 0; i < numHits; ++i)
			{
				hits[i].collider.gameObject.GetComponents(receiversBeforeFilter);
				if (receiversBeforeFilter.Count != 0 && hits[i].distance < minDist)
				{
					// filter hit
					potentialReceiversA.Clear();
					foreach (var receiver in receiversBeforeFilter)
					{
						if (isRaycastReceiverIncluded.Count > 0 && !isRaycastReceiverIncluded[^1](receiver))
							continue;
						potentialReceiversA.Add(receiver);
					}

					if (potentialReceiversA.Count == 0)
						continue;

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

		bool IRaycastController.CompareTag(string other) => gameObject.CompareTag(other);
        bool IRaycastController.isPressed => (pressed.phase == InputActionPhase.Started);
		bool IRaycastController.wasPressedThisFrame => (pressStartFrame == Time.frameCount);
		Vector3 IRaycastController.position => transform.position;
		Vector3 IRaycastController.forward => transform.forward;
    }
}
