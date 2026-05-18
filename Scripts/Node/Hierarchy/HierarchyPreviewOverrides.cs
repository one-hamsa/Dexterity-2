using System;
using System.Collections.Generic;

namespace OneHamsa.Dexterity
{
    /// <summary>
    /// Per-source override registry consulted by <see cref="IDexteritySource.IsActive"/>.
    /// When a source has an override, IsActive returns the overridden value instead of
    /// the subclass's computed value. Used by editor tooling to "pretend the scene state
    /// is whatever the user wants" — works at edit time and at runtime.
    ///
    /// Keys are <see cref="IDexteritySource"/> so both providers (leaves) and aggregators
    /// (intermediates) can be forced — handy for debugging mid-graph signals.
    /// </summary>
    public static class HierarchyPreviewOverrides
    {
        private static readonly Dictionary<IDexteritySource, bool> s_overrides = new();

        /// <summary>Fired after any override changes (set / cleared / cleared-all).</summary>
        public static event Action onChanged;

        public static bool TryGet(IDexteritySource source, out bool value)
            => s_overrides.TryGetValue(source, out value);

        public static bool HasOverride(IDexteritySource source)
            => s_overrides.ContainsKey(source);

        public static void Set(IDexteritySource source, bool value)
        {
            s_overrides[source] = value;
            source.NotifyExternalChanged();
            onChanged?.Invoke();
        }

        public static void Clear(IDexteritySource source)
        {
            if (!s_overrides.Remove(source)) return;
            source.NotifyExternalChanged();
            onChanged?.Invoke();
        }

        public static void ClearAll()
        {
            if (s_overrides.Count == 0) return;

            var snapshot = new List<IDexteritySource>(s_overrides.Keys);
            s_overrides.Clear();
            foreach (var s in snapshot)
                if (s != null) s.NotifyExternalChanged();

            onChanged?.Invoke();
        }

        public static IEnumerable<KeyValuePair<IDexteritySource, bool>> All => s_overrides;
    }
}
