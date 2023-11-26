using UnityEngine;

namespace OneHamsa.Dexterity.Builtins
{
    public class WhitelistRaycastFilter : MonoBehaviour
    {
        private RaycastController.RaycastFilter filter;

        private void OnEnable()
        {
            filter = RaycastController.AddFilter(transform);
        }
        
        private void OnDisable()
        {
            RaycastController.RemoveFilter(filter);
        }
    }
}
