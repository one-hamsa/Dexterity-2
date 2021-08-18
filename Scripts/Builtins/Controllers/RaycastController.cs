using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    public class RaycastController : MonoBehaviour, IRaycastController
	{
		const float rayLength = 100f;
		const int maxHits = 20;

		public LayerMask layerMask = int.MaxValue;
		public InputAction pressed;

		public Ray ray { get; private set; }
		public bool didHit { get; private set; }
		public RaycastHit hit { get; private set; }

		RaycastHit[] hits = new RaycastHit[maxHits];
		List<IRaycastReceiver> lastReceivers = new List<IRaycastReceiver>(4);
		List<IRaycastReceiver> receivers = new List<IRaycastReceiver>(4);

        private void Awake()
        {
			pressed.Enable();
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
				hits[i].collider.gameObject.GetComponents(receivers);
				if (receivers.Count != 0 && hits[i].distance < minDist)
				{
					closestReceivers = receivers;
					hit = hits[i];
					minDist = hits[i].distance;
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

		public bool isPressed => pressed.phase == InputActionPhase.Started;
    }
}
