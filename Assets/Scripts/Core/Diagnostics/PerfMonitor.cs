using System;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace KitchenDesigner.Core
{
    [DefaultExecutionOrder(RunsAfterEveryMeasuredComponent)]
    public class PerfMonitor : MonoBehaviour
    {
        internal const int RunsAfterEveryMeasuredComponent = 30000;

        private const int DumpEveryFrames = 120;

        internal const int CsvCapacityFrames = 3600;
        internal const float DumpThresholdMs = 0.05f;
        internal const int TopMarkersInDump = 14;
        internal const int DumpNameColumnWidth = 42;
        internal const int HudNameColumnWidth = 40;
        internal const int MillisecondsColumnWidth = 6;
        internal const float HudRefreshSeconds = 0.25f;

        private static readonly float[] HistogramEdgesMs = { 8f, 16f, 33f, 50f };
        private static readonly string[] HistogramLabels = { "<8", "8-16", "16-33", "33-50", ">50" };

        public static PerfMonitor? Instance { get; private set; }

        private static bool _enabled;

        public static bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                PerfMarkers.Measuring = value;
            }
        }

        public string HudText { get; private set; } = "";

        internal struct MarkerSlot
        {
            public string Name;
            public float PreviousFrameMs;
            public float SumMs;
            public float MaxMs;
            public int FramesMeasured;

            internal void RecordFrame(float ms)
            {
                PreviousFrameMs = ms;
                SumMs += ms;
                if (ms > MaxMs) MaxMs = ms;
                FramesMeasured++;
            }

            internal float AverageMsPerFrame => FramesMeasured > 0 ? SumMs / FramesMeasured : 0f;
        }

        private MarkerSlot[]? _slots;

        private ProfilerRecorder _gcFrame;
        private ProfilerRecorder _gcUsed;
        private ProfilerRecorder _mainThread;
        private ProfilerRecorder _drawCalls;
        private ProfilerRecorder _batches;
        private ProfilerRecorder _setPass;

        private int _windowFrames;
        private float _dtSumMs, _dtMaxMs;
        private readonly int[] _histogram = new int[HistogramLabels.Length];
        private double _gcSumBytes;
        private float _gcMaxBytes;
        private int _getAllSum;

        private int _worstFrame;
        private float _worstDtMs;
        private float _worstGcBytes;
        private float[]? _worstMarkersMs;

        private PerfCsvLog? _csv;
        private float[]? _row;
        private float _nextHudTime;

        private const int CsvFixedColumns = 9;

        private void Awake()
        {
            Instance = this;

            var names = PerfMarkers.NamesInDeclarationOrder;

            _slots = new MarkerSlot[names.Count];
            for (int i = 0; i < names.Count; i++)
                _slots[i] = new MarkerSlot { Name = names[i] };

            _worstMarkersMs = new float[names.Count];

            _gcFrame = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _gcUsed = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory");
            _mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
            _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            _setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");

            var header = new string[CsvFixedColumns + names.Count];
            header[0] = "frame";
            header[1] = "dt_ms";
            header[2] = "gc_bytes";
            header[3] = "gc_used_mb";
            header[4] = "main_ms";
            header[5] = "draw_calls";
            header[6] = "batches";
            header[7] = "setpass";
            header[8] = "getall_calls";
            for (int i = 0; i < names.Count; i++) header[CsvFixedColumns + i] = names[i];

            _csv = new PerfCsvLog(header, CsvCapacityFrames);
            _row = new float[header.Length];

            StartWindow();

            if (GetComponent<PerfHud>() == null) gameObject.AddComponent<PerfHud>();
        }

        private void Update() => HandleHotkeys();

        private void LateUpdate()
        {
            if (!Enabled) return;

            Sample();
            if (++_windowFrames >= DumpEveryFrames) Dump();
        }

        internal void SimulateLateUpdateForTests() => LateUpdate();

        private void HandleHotkeys()
        {
            if (!Input.GetKeyDown(KeyCode.F9)) return;

            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            ApplyF9(shift);
        }

        internal void ApplyF9(bool shiftHeld)
        {
            if (shiftHeld) ToggleCsv();
            else
            {
                Enabled = !Enabled;
                if (Enabled) StartWindow();
                Debug.Log($"[Perf] замер {(Enabled ? "включён" : "выключен")}");
            }
        }

        internal void SimulateF9ForTests(bool shiftHeld = false) => ApplyF9(shiftHeld);

        private void ToggleCsv() => SetCsvRecording(_csv == null || !_csv.Recording);

        public string? SetCsvRecording(bool on)
        {
            if (_csv == null) return null;

            if (!on)
            {
                if (!_csv.Recording) return null;
                int rows = _csv.Rows;
                var path = _csv.Stop();
                Debug.Log(path != null
                    ? $"[Perf] CSV: {rows} кадров -> {path}"
                    : "[Perf] CSV: писать нечего");
                return path;
            }

            if (_csv.Recording) return null;

            if (!Enabled)
            {
                Enabled = true;
                StartWindow();
            }
            _csv.Start();
            Debug.Log($"[Perf] запись CSV пошла (до {CsvCapacityFrames} кадров, Shift+F9 — стоп)");
            return null;
        }

        private void Sample()
        {
            if (_slots == null) return;

            for (int i = 0; i < _slots.Length; i++)
                _slots[i].RecordFrame(PerfMarkers.TakeFrameMs(i));

            float dtMs = Time.unscaledDeltaTime * 1000f;
            float gcBytes = _gcFrame.Valid ? _gcFrame.LastValue : 0f;

            _dtSumMs += dtMs;
            if (dtMs > _dtMaxMs) _dtMaxMs = dtMs;
            _histogram[HistogramBucket(dtMs)]++;
            _gcSumBytes += gcBytes;
            if (gcBytes > _gcMaxBytes) _gcMaxBytes = gcBytes;

            int getAll = PartRegistryInstance.TakeGetAllCalls();
            _getAllSum += getAll;

            if (dtMs > _worstDtMs)
            {
                _worstDtMs = dtMs;
                _worstFrame = Time.frameCount;
                _worstGcBytes = gcBytes;
                if (_worstMarkersMs != null)
                    for (int i = 0; i < _slots.Length; i++) _worstMarkersMs[i] = _slots[i].PreviousFrameMs;
            }

            WriteCsvRow(dtMs, gcBytes, getAll);

            if (Time.unscaledTime >= _nextHudTime)
            {
                _nextHudTime = Time.unscaledTime + HudRefreshSeconds;
                HudText = BuildHudText(dtMs, gcBytes);
            }
        }

        private void WriteCsvRow(float dtMs, float gcBytes, int getAllCalls)
        {
            if (_csv == null || !_csv.Recording || _row == null || _slots == null) return;

            _row[0] = Time.frameCount;
            _row[1] = dtMs;
            _row[2] = gcBytes;
            _row[3] = _gcUsed.Valid ? _gcUsed.LastValue / (1024f * 1024f) : 0f;
            _row[4] = _mainThread.Valid ? _mainThread.LastValue * 1e-6f : 0f;
            _row[5] = _drawCalls.Valid ? _drawCalls.LastValue : 0f;
            _row[6] = _batches.Valid ? _batches.LastValue : 0f;
            _row[7] = _setPass.Valid ? _setPass.LastValue : 0f;
            _row[8] = getAllCalls;
            for (int i = 0; i < _slots.Length; i++) _row[CsvFixedColumns + i] = _slots[i].PreviousFrameMs;

            _csv.Append(_row);
            if (!_csv.Recording)
                Debug.LogWarning($"[Perf] CSV-буфер заполнен ({CsvCapacityFrames} кадров), запись остановлена — Shift+F9 сохранит файл");
        }

        private static int HistogramBucket(float dtMs)
        {
            for (int i = 0; i < HistogramEdgesMs.Length; i++)
                if (dtMs < HistogramEdgesMs[i]) return i;
            return HistogramEdgesMs.Length;
        }

        private void Dump()
        {
            if (_slots == null || _windowFrames == 0) return;

            var sb = new StringBuilder();
            sb.AppendLine($"[Perf] == кадр {Time.frameCount}, окно {_windowFrames} кадров ==");
            sb.Append($"  dt avg={_dtSumMs / _windowFrames:F1}ms  max={_dtMaxMs:F1}ms   ");
            for (int i = 0; i < _histogram.Length; i++)
                sb.Append($"{HistogramLabels[i]}:{_histogram[i]} ");
            sb.AppendLine();

            sb.AppendLine($"  GC/кадр avg={_gcSumBytes / _windowFrames / 1024.0:F1}КБ  max={_gcMaxBytes / 1024f:F1}КБ" +
                          $"   куча={(_gcUsed.Valid ? _gcUsed.LastValue / (1024f * 1024f) : 0f):F1}МБ" +
                          $"   draw={(_drawCalls.Valid ? _drawCalls.LastValue : 0)}" +
                          $"  batches={(_batches.Valid ? _batches.LastValue : 0)}" +
                          $"  setpass={(_setPass.Valid ? _setPass.LastValue : 0)}" +
                          $"   GetAll/кадр={(float)_getAllSum / _windowFrames:F1}");

            AppendWorstFrameOfTheWindow(sb);
            AppendWindowTop(sb, _slots);

            Debug.Log(sb.ToString());
            StartWindow();
        }

        private void AppendWorstFrameOfTheWindow(StringBuilder sb)
        {
            if (_slots == null || _worstMarkersMs == null || _worstFrame == 0) return;

            sb.AppendLine($"  худший кадр #{_worstFrame}: dt={_worstDtMs:F1}ms  gc={_worstGcBytes / 1024f:F1}КБ");
            var order = IndicesByDescendingValue(_worstMarkersMs);
            int shown = 0;
            for (int k = 0; k < order.Length && shown < TopMarkersInDump; k++)
            {
                int i = order[k];
                if (!WorthShowing(_worstMarkersMs[i])) break;
                sb.AppendLine("     " + MarkerLine(_slots[i].Name, _worstMarkersMs[i], DumpNameColumnWidth));
                shown++;
            }
            if (shown == 0) sb.AppendLine($"     все маркеры дешевле {DumpThresholdMs:F2}ms — время уходит мимо них");
        }

        internal static void AppendWindowTop(StringBuilder sb, MarkerSlot[] slots)
        {
            var avgPerFrameMs = new float[slots.Length];
            for (int i = 0; i < slots.Length; i++) avgPerFrameMs[i] = slots[i].AverageMsPerFrame;

            sb.AppendLine("  за окно (avg / max):");
            var order = IndicesByDescendingValue(avgPerFrameMs);
            int shown = 0;
            for (int k = 0; k < order.Length && shown < TopMarkersInDump; k++)
            {
                int i = order[k];
                if (!WorthShowing(avgPerFrameMs[i]) && !WorthShowing(slots[i].MaxMs)) continue;
                sb.AppendLine("     " + MarkerLine(slots[i].Name, avgPerFrameMs[i], DumpNameColumnWidth)
                              + " / " + Milliseconds(slots[i].MaxMs) + "ms");
                shown++;
            }
        }

        internal static bool WorthShowing(float ms) => ms >= DumpThresholdMs;

        internal static string MarkerLine(string name, float ms, int nameWidth) =>
            name.PadRight(nameWidth) + " " + Milliseconds(ms) + "ms";

        private static string Milliseconds(float ms) =>
            ms.ToString("F2").PadLeft(MillisecondsColumnWidth);

        internal static int[] IndicesByDescendingValue(float[] values)
        {
            var idx = new int[values.Length];
            for (int i = 0; i < idx.Length; i++) idx[i] = i;
            for (int i = 1; i < idx.Length; i++)
            {
                int cur = idx[i];
                int j = i - 1;
                while (j >= 0 && values[idx[j]] < values[cur]) { idx[j + 1] = idx[j]; j--; }
                idx[j + 1] = cur;
            }
            return idx;
        }

        private string BuildHudText(float dtMs, float gcBytes)
        {
            if (_slots == null) return "";

            var sb = new StringBuilder(256);
            sb.AppendLine($"dt {dtMs,5:F1}ms ({(dtMs > 0.01f ? 1000f / dtMs : 0f):F0} fps)   худший {_dtMaxMs:F1}ms");
            sb.AppendLine($"GC {gcBytes / 1024f,7:F1}КБ/кадр   draw {(_drawCalls.Valid ? _drawCalls.LastValue : 0)}" +
                          $"   GetAll {(_windowFrames > 0 ? (float)_getAllSum / _windowFrames : 0f):F1}/кадр");

            var previousFrameMs = new float[_slots.Length];
            for (int i = 0; i < _slots.Length; i++) previousFrameMs[i] = _slots[i].PreviousFrameMs;
            var order = IndicesByDescendingValue(previousFrameMs);
            for (int k = 0; k < 3 && k < order.Length; k++)
            {
                int i = order[k];
                if (!WorthShowing(previousFrameMs[i])) break;
                sb.AppendLine(MarkerLine(_slots[i].Name, previousFrameMs[i], HudNameColumnWidth));
            }

            if (_csv != null && _csv.Recording) sb.AppendLine($"● запись CSV: {_csv.Rows} кадров");
            return sb.ToString();
        }

        public static void DumpNow()
        {
            if (Instance != null) Instance.Dump();
        }

        private void StartWindow()
        {
            _windowFrames = 0;
            _dtSumMs = 0f;
            _dtMaxMs = 0f;
            _gcSumBytes = 0.0;
            _gcMaxBytes = 0f;
            _getAllSum = 0;
            _worstFrame = 0;
            _worstDtMs = 0f;
            _worstGcBytes = 0f;
            Array.Clear(_histogram, 0, _histogram.Length);
            PerfMarkers.DropEverythingMeasuredSoFar();

            if (_slots == null) return;
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i].SumMs = 0f;
                _slots[i].MaxMs = 0f;
                _slots[i].FramesMeasured = 0;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (_gcFrame.Valid) _gcFrame.Dispose();
            if (_gcUsed.Valid) _gcUsed.Dispose();
            if (_mainThread.Valid) _mainThread.Dispose();
            if (_drawCalls.Valid) _drawCalls.Dispose();
            if (_batches.Valid) _batches.Dispose();
            if (_setPass.Valid) _setPass.Dispose();
        }
    }
}
