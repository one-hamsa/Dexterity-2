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
        [SerializeField] bool scale;

        private float _width;
        private float _destWidth;
        
        protected RaycastController controller;
        
        protected virtual bool isVisible => isActiveAndEnabled;

        protected virtual void Awake()
        {
            controller = GetComponent<RaycastController>();
            lineRenderer.useWorldSpace = false;
            destinationLineRenderer.useWorldSpace = false;
            _width = lineRenderer.widthMultiplier;
            _destWidth = destinationLineRenderer.widthMultiplier;
        }

        protected virtual void OnEnable() { }

        protected virtual void OnDisable() => Update();

        // Update is called once per frame
        private float _length;
        protected virtual void Update() {
            if (controller.didHit)
                _length = Vector3.Distance(controller.ray.origin, controller.hit.point);
            UpdateSourceLineRenderer();
            UpdateDestinationLineRenderer();
        }
        
        private float scaleFactor {
            get {
                Vector3 scale = transform.lossyScale;
                return (scale.x + scale.y + scale.z) / 3f;
            }
        }
        
        private void UpdateSourceLineRenderer()
        {
            if (lineRenderer == null)
                return;

            float maxLength = this.maxLength;
            if (scale) {
                float s = scaleFactor;
                maxLength *= s;
                lineRenderer.widthMultiplier = _width * s;
            }
            
            float length = controller.didHit ? Mathf.Min(_length, maxLength) : maxLength;
            Vector3 point0 = Vector3.zero;
            Vector3 point1 = transform.InverseTransformPoint(controller.ray.GetPoint(length));
            
            lineRenderer.enabled = controller.current && controller.showVisibleRay && isVisible;
            lineRenderer.SetPosition(0, point0);
            lineRenderer.SetPosition(1, point1);
        }

        private void UpdateDestinationLineRenderer()
        {
            if (destinationLineRenderer == null)
                return;
            
            destinationLineRenderer.enabled = controller.current && controller.didHit && isActiveAndEnabled;

            float maxDestLength = this.maxDestLength;
            if (scale) {
                float f = scaleFactor;
                maxDestLength *= f;
                destinationLineRenderer.widthMultiplier = _destWidth * f;
            }
            
            float length = Mathf.Min(_length, maxDestLength);
            Vector3 point0 = transform.InverseTransformPoint(controller.ray.GetPoint(_length));
            Vector3 point1 = transform.InverseTransformPoint(controller.ray.GetPoint(_length - length * .01f));
            Vector3 point2 = transform.InverseTransformPoint(controller.ray.GetPoint(_length - length));
            
            destinationLineRenderer.SetPosition(0, point0);
            destinationLineRenderer.SetPosition(1, point1); 
            destinationLineRenderer.SetPosition(2, point2);
        }
    }
}
