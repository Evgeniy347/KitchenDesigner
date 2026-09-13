using System;
using System.Collections.Generic;

namespace KitchenDesigner.Core
{
    public sealed class AlignedFrameRow
    {
        private readonly float[] _markersMs;

        private bool _workIsIn;
        private bool _countersAreIn;

        public AlignedFrameRow(int markerCount)
        {
            _markersMs = new float[markerCount < 0 ? 0 : markerCount];
        }

        public int Frame { get; private set; }
        public int GetAllCalls { get; private set; }
        public int NamedScans { get; private set; }
        public string Scans { get; private set; } = string.Empty;
        public string SelectionWork { get; private set; } = string.Empty;
        public string Listeners { get; private set; } = string.Empty;
        public IReadOnlyList<float> MarkersMs => _markersMs;

        public float DtMs { get; private set; }
        public float MainMs { get; private set; }
        public float GcBytes { get; private set; }
        public float GcUsedMb { get; private set; }
        public float DrawCalls { get; private set; }
        public float Batches { get; private set; }
        public float SetPassCalls { get; private set; }

        public bool Complete => _workIsIn && _countersAreIn;

        public int FramesLost { get; private set; }

        public void RememberTheWorkOfTheFrameThatJustRan(int frame, int getAllCalls, int namedScans,
            string? scans, string? selectionWork, string? listeners, IReadOnlyList<float>? markersMs)
        {
            if (_workIsIn && !_countersAreIn) FramesLost++;

            Frame = frame;
            GetAllCalls = getAllCalls;
            NamedScans = namedScans;
            Scans = scans ?? string.Empty;
            SelectionWork = selectionWork ?? string.Empty;
            Listeners = listeners ?? string.Empty;

            int taken = markersMs == null ? 0 : Math.Min(_markersMs.Length, markersMs.Count);
            for (int i = 0; i < taken; i++) _markersMs[i] = markersMs![i];
            for (int i = taken; i < _markersMs.Length; i++) _markersMs[i] = 0f;

            _workIsIn = true;
            _countersAreIn = false;
        }

        public bool TakeTheCountersTheEngineReportsAFrameLate(float dtMs, float mainMs, float gcBytes,
            float gcUsedMb, float drawCalls, float batches, float setPassCalls)
        {
            if (!_workIsIn || _countersAreIn) return false;

            DtMs = dtMs;
            MainMs = mainMs;
            GcBytes = gcBytes;
            GcUsedMb = gcUsedMb;
            DrawCalls = drawCalls;
            Batches = batches;
            SetPassCalls = setPassCalls;

            _countersAreIn = true;
            return true;
        }

        public void Forget()
        {
            _workIsIn = false;
            _countersAreIn = false;
            Frame = 0;
            GetAllCalls = 0;
            NamedScans = 0;
            Scans = string.Empty;
            SelectionWork = string.Empty;
            Listeners = string.Empty;
            DtMs = 0f;
            MainMs = 0f;
            GcBytes = 0f;
            GcUsedMb = 0f;
            DrawCalls = 0f;
            Batches = 0f;
            SetPassCalls = 0f;
            Array.Clear(_markersMs, 0, _markersMs.Length);
        }
    }
}
