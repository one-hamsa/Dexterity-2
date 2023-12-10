using System;
using System.Diagnostics;
using UnityEngine.Profiling;

namespace OneHamsa.Dexterity
{
    internal struct ScopedProfile : IDisposable
    {
        public ScopedProfile(string name) => Profiler.BeginSample(name);
        void IDisposable.Dispose() => Profiler.EndSample();
    }
}