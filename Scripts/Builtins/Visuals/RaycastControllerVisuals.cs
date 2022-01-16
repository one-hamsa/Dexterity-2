using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OneHamsa.Dexterity.Visual.Builtins
{
    [RequireComponent(typeof(RaycastController))]
    public class RaycastControllerVisuals : MonoBehaviour
    {
        [SerializeField] LineRenderer lineRenderer;
        [SerializeField] float maxLength = 10;

        RaycastController controller;

        private void Awake()
        {
            controller = GetComponent<RaycastController>();
        }
        // Update is called once per frame
        void Update()
        {
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
    }
}
