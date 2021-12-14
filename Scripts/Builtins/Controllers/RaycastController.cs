using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class RaycastController : MonoBehaviour, IRaycastController
	{
		const float rayLength = 100f;
		const int maxHits = 20;

		public LayerMask layerMask = int.MaxValue;
		public InputAction pressed;

		[Header("Debug")]
		public Color debugColliderColor = new Color(1f, .5f, 0f);
		public Color debugHitColor = new Color(1f, .25f, 0f);

		public Ray ray { get; private set; }
		public bool didHit { get; private set; }
		public RaycastHit hit { get; private set; }

		RaycastHit[] hits = new RaycastHit[maxHits];
		List<IRaycastReceiver> lastReceivers = new List<IRaycastReceiver>(4), 
		potentialReceiversA = new List<IRaycastReceiver>(4), 
		potentialReceiversB = new List<IRaycastReceiver>(4);

        private void OnEnable()
        {
			pressed.Enable();
		}
		private void OnDisable()
        {
			pressed.Disable();
		}

        void Update()
        {
			Vector3 origin = transform.position;
			Vector3 direction = transform.forward;

			didHit = false;

			ray = new Ray(origin, direction);
			Debug.DrawRay(ray.origin, ray.direction * rayLength, Color.blue);
					
			int numHits = Physics.RaycastNonAlloc(ray, hits, rayLength, layerMask);

			float minDist = float.PositiveInfinity;
			hit = new RaycastHit();
			List<IRaycastReceiver> closestReceivers = null;

			for (int i = 0; i < numHits; ++i)
			{
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
				if (closestReceivers == null || !closestReceivers.Contains(receiver))
					receiver?.ClearHit(this);
			}
			lastReceivers.Clear();

			if (closestReceivers != null)
            {
				didHit = true;
				foreach (var receiver in closestReceivers)
				{
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

        bool IRaycastController.isPressed => pressed.phase == InputActionPhase.Started;
		Vector3 IRaycastController.position => transform.position;
		Quaternion IRaycastController.rotation => transform.rotation;
    }
}
