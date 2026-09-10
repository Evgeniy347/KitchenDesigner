using System;
using System.Diagnostics;
using Unity.Profiling;

namespace KitchenDesigner.Core
{
    public readonly struct PerfMarker
    {
        private readonly int _slot;
        private readonly ProfilerMarker _unity;

        internal PerfMarker(int slot, ProfilerMarker unity)
        {
            _slot = slot;
            _unity = unity;
        }

        internal int Slot => _slot;

        public Scope Auto() => new Scope(_slot, _unity);

        public readonly struct Scope : IDisposable
        {
            private readonly int _slot;
            private readonly long _startedAtTicks;
            private readonly bool _hasStartTicks;
            private readonly ProfilerMarker _unity;

            internal Scope(int slot, ProfilerMarker unity)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KD_PERF
                _slot = slot;
                _unity = unity;
                _hasStartTicks = PerfMarkers.Measuring;
                _startedAtTicks = _hasStartTicks ? Stopwatch.GetTimestamp() : 0L;
                _unity.Begin();
#else
                _slot = 0;
                _unity = default;
                _hasStartTicks = false;
                _startedAtTicks = 0L;
#endif
            }

            public void Dispose()
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || KD_PERF
                _unity.End();
                if (!_hasStartTicks) return;
                PerfMarkers.AddTicks(_slot, Stopwatch.GetTimestamp() - _startedAtTicks);
#endif
            }
        }
    }
}
