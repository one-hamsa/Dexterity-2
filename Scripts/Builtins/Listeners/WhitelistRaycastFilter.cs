using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OneHamsa.Dexterity.Builtins
{
    public class WhitelistRaycastFilter : MonoBehaviour
    {
        public List<Transform> transforms = new();
        public bool includeSelf = true;

        private RaycastController.RaycastFilter filter;

        private HashSet<Transform> allTransforms = new();

        private void OnEnable()
        {
            allTransforms.Clear();
            allTransforms.UnionWith(transforms);
            if (includeSelf)
                allTransforms.Add(transform);

            var allFilters = allTransforms.Select(t => RaycastController.CreateTransformFilter(t)).ToList();
            filter = RaycastController.AddFilter(t =>
            {
                foreach (var f in allFilters)
                {
                    if (f(t))
                        return true;
                }

                return false;
            });
        }

        private void OnDisable()
        {
            RaycastController.RemoveFilter(filter);
        }
    }
}
