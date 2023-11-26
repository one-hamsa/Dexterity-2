using UnityEngine;

namespace OneHamsa.Dexterity.Builtins
{
    public class BlacklistRaycastFilter : MonoBehaviour
    {
        private RaycastController.RaycastFilter filter;

        private void OnEnable()
        {
            filter = RaycastController.AddBlockingFilter(transform);
        }
        
        private void OnDisable()
        {
            RaycastController.RemoveFilter(filter);
        }
    }
}
