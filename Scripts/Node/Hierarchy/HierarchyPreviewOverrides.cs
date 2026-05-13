using System;
using System.Collections.Generic;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// Per-<see cref="HierarchyStateProvider"/> override registry consulted by
    /// <see cref="HierarchyStateProvider.IsActive"/>. When a provider has an
    /// override, IsActive returns the overridden value instead of the subclass's
    /// computed value. Used by the graph window to "pretend the scene state is
    /// whatever the user wants" — works at edit time and at runtime.
    /// </summary>
    public static class HierarchyPreviewOverrides
    {
        private static readonly Dictionary<HierarchyStateProvider, bool> s_overrides = new();

        /// <summary>Fired after any override changes (set / cleared / cleared-all).</summary>
        public static event Action onChanged;

        public static bool TryGet(HierarchyStateProvider p, out bool value)
            => s_overrides.TryGetValue(p, out value);

        public static bool HasOverride(HierarchyStateProvider p)
            => s_overrides.ContainsKey(p);

        public static void Set(HierarchyStateProvider p, bool value)
        {
            s_overrides[p] = value;
            p.NotifyExternalChanged();
            onChanged?.Invoke();
        }

        public static void Clear(HierarchyStateProvider p)
        {
            if (!s_overrides.Remove(p)) return;
            p.NotifyExternalChanged();
            onChanged?.Invoke();
        }

        public static void ClearAll()
        {
            if (s_overrides.Count == 0) return;

            var snapshot = new List<HierarchyStateProvider>(s_overrides.Keys);
            s_overrides.Clear();
            foreach (var p in snapshot)
                if (p != null) p.NotifyExternalChanged();

            onChanged?.Invoke();
        }

        public static IEnumerable<KeyValuePair<HierarchyStateProvider, bool>> All => s_overrides;
    }
}
