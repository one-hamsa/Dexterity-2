using System;
using System.Diagnostics;
using UnityEngine.Profiling;

namespace OneHamsa.Dexterity
{
    internal struct ScopedProfile : IDisposable
    {
        public ScopedProfile(string name) => Profiler.BeginSample(name);
        // Public Dispose (not explicit IDisposable.Dispose) so `using` binds directly on the struct
        // without boxing it — IL2CPP boxes explicit interface impls here even though Mono doesn't.
        public void Dispose() => Profiler.EndSample();
    }
}