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
            private readonly ProfilerMarker _unity;

            internal Scope(int slot, ProfilerMarker unity)
            {
                _slot = slot;
                _unity = unity;
                _startedAtTicks = PerfMarkers.Measuring ? Stopwatch.GetTimestamp() : 0L;
                _unity.Begin();
            }

            public void Dispose()
            {
                _unity.End();
                if (_startedAtTicks == 0L) return;
                PerfMarkers.AddTicks(_slot, Stopwatch.GetTimestamp() - _startedAtTicks);
            }
        }
    }
}
