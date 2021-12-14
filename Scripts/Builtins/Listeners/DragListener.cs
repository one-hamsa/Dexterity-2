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
        public bool dragging { get; private set; }
        public Vector3 dragPositionOffset {
            get {
                if (!dragging)
                    return default;
                return draggingController.position - controllerStartPosition;
            }
        }

        private ClickListener clickListener;
        public readonly HashSet<IRaycastController> controllers = new HashSet<IRaycastController>();
        private RaycastHit lastHit;
        private IRaycastController draggingController;
        private Vector3 controllerStartPosition;
        private Quaternion controllerStartRotation;

        void Awake()
        {
            clickListener = GetComponent<ClickListener>();
        }

        private void OnEnable() {
            clickListener.onPressDown += HandlePressDown;
            clickListener.onPressUp += HandlePressUp;
        }

        private void OnDisable() {
            clickListener.onPressDown -= HandlePressDown;
            clickListener.onPressUp -= HandlePressUp;
        }

        private void HandlePressDown()
        {
            if (controllers.Count > 0) {
                dragging = true;
                draggingController = controllers.First();
                controllerStartPosition = draggingController.position;
                controllerStartRotation = draggingController.rotation;
                onDragStarted?.Invoke();
            }
        }
        
        private void HandlePressUp()
        {
            if (dragging) {
                dragging = false;
                onDragEnded?.Invoke();
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
    }
}
