using System;
using UnityEngine;

namespace OneHamsa.Dexterity.Builtins
{
    [RequireComponent(typeof(RaycastController))]
    public class RaycastControllerVisuals : MonoBehaviour
    {
        [SerializeField] LineRenderer lineRenderer;
        [SerializeField] LineRenderer destinationLineRenderer;
        [SerializeField] float maxLength = 10;
        [SerializeField] float maxDestLength = 1;

        RaycastController controller;

        protected virtual bool isVisible => isActiveAndEnabled;

        private void Awake()
        {
            controller = GetComponent<RaycastController>();
            lineRenderer.useWorldSpace = false;
            destinationLineRenderer.useWorldSpace = false;
        }

        protected virtual void OnEnable() { }

        protected virtual void OnDisable() => Update();

        // Update is called once per frame
        protected virtual void Update()
        {
            UpdateSourceLineRenderer();
            UpdateDestinationLineRenderer();
        }

        private void UpdateSourceLineRenderer()
        {
            if (lineRenderer == null)
                return;
            
            float length = controller.didHit ? Mathf.Min(controller.hit.distance, maxLength) : maxLength;
            Vector3 point0 = Vector3.zero;
            Vector3 point1 = length * Vector3.forward;
            
            lineRenderer.enabled = controller.current && isVisible;
            lineRenderer.SetPosition(0, point0);
            lineRenderer.SetPosition(1, point1);
        }

        private void UpdateDestinationLineRenderer()
        {
            if (destinationLineRenderer == null)
                return;
            
            destinationLineRenderer.enabled = controller.current && controller.didHit && isActiveAndEnabled;
            
            float length = Mathf.Min(controller.hit.distance, maxDestLength);
            Vector3 point0 = Vector3.forward * controller.hit.distance;
            Vector3 point1 = point0 - Vector3.forward * (length * .01f);
            Vector3 point2 = point0 - Vector3.forward * length;
            
            destinationLineRenderer.SetPosition(0, point0);
            destinationLineRenderer.SetPosition(1, point1); 
            destinationLineRenderer.SetPosition(2, point2);
        }
    }
}
