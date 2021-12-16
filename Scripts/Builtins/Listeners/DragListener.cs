using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    [RequireComponent(typeof(ClickListener))]
    public class DragListener : MonoBehaviour, IRaycastReceiver
    {
        public event Action onDragStarted;
        public event Action onDragEnded;
        public bool dragging => draggingController != null;

        private ClickListener clickListener;
        public readonly HashSet<IRaycastController> controllers = new HashSet<IRaycastController>();
        public RaycastHit dragStartHit { get; private set; }
        public IRaycastController draggingController { get; private set; }
        private RaycastHit lastHit;
        private Vector3 dragStartControllerPosition;
        private Vector3 dragStartControllerForward;

        void Awake()
        {
            clickListener = GetComponent<ClickListener>();
        }

        private void OnEnable() {
            clickListener.onPressDown += HandlePressDown;
        }

        private void OnDisable() {
            clickListener.onPressDown -= HandlePressDown;
        }

        private void HandlePressDown()
        {
            if (controllers.Count > 0) {
                var controller = controllers.First();

                if (controller.Lock(this)) {
                    draggingController = controller;
                    dragStartHit = lastHit;
                    dragStartControllerPosition = draggingController.position;
                    dragStartControllerForward = draggingController.forward;
                    onDragStarted?.Invoke();
                }
            }
        }

        public void ReceiveHit(IRaycastController controller, RaycastHit hit)
        {
            controllers.Add(controller);
            lastHit = hit;
        }

        public void ClearHit(IRaycastController controller)
        {
            controllers.Remove(controller);
        }

        private void Update() {
            if (dragging && !draggingController.isPressed) {
                if (!draggingController.Unlock(this)) {
                    Debug.LogError("Failed to unlock dragging controller");
                }
                
                draggingController = null;
                onDragEnded?.Invoke();
            }
        }
    }
}
