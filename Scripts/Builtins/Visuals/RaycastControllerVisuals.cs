using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    [RequireComponent(typeof(RaycastController))]
    public class RaycastControllerVisuals : MonoBehaviour
    {
        [SerializeField] LineRenderer lineRenderer;
        [SerializeField] LineRenderer destinationLineRenderer;
        [SerializeField] float maxLength = 10;
        [SerializeField] float maxDestLength = 1;

        RaycastController controller;

        private void Awake()
        {
            controller = GetComponent<RaycastController>();
        }
        // Update is called once per frame
        void Update()
        {
            UpdateSourceLineRenderer();
            UpdateDestinationLineRenderer();
        }

        private void UpdateSourceLineRenderer()
        {
            if (lineRenderer == null)
                return;
            
            lineRenderer.enabled = controller.current;
            
            Vector3 origin = controller.ray.origin;
            lineRenderer.SetPosition(0, origin);
            if (controller.didHit)
            {
                float hitDistance = Vector3.Distance(origin, controller.hit.point);
                hitDistance = Mathf.Min(maxLength, hitDistance);
                lineRenderer.SetPosition(1, controller.ray.origin + controller.ray.direction * hitDistance);
            }
            else
            {
                lineRenderer.SetPosition(1, controller.ray.origin + controller.ray.direction * maxLength);
            }
        }

        private void UpdateDestinationLineRenderer()
        {
            if (destinationLineRenderer == null)
                return;
            
            destinationLineRenderer.enabled = controller.current && controller.didHit;

            var destToOrigin = controller.ray.origin - controller.hit.point;
            if (destToOrigin.sqrMagnitude > maxDestLength * maxDestLength)
                destToOrigin = destToOrigin.normalized * maxDestLength;
            
            destinationLineRenderer.SetPosition(0, controller.hit.point);
            destinationLineRenderer.SetPosition(1, controller.hit.point + destToOrigin * .01f); 
            destinationLineRenderer.SetPosition(2, controller.hit.point + destToOrigin); 
        }
    }
}
