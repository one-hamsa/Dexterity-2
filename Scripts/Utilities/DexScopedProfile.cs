using System;
using System.Diagnostics;
using UnityEngine.Profiling;

namespace OneHumus
{
    public struct DexScopedProfile : IDisposable
    {
        public DexScopedProfile(string name) => Profiler.BeginSample(name);
        void IDisposable.Dispose() => Profiler.EndSample();
    }
}