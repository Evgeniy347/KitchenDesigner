#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Трассировка кадра в dev-сборках: время маркеров из <see cref="PerfMarkers"/>,
    /// счётчики движка (GC, рендер), гистограмма dt и разбор худшего кадра окна.
    ///
    /// Выключен по умолчанию (иначе дампы засоряют лог PlayMode-тестов, которые тоже
    /// поднимают Bootstrap). F9 — включить замер и экранный HUD, Shift+F9 — запись CSV
    /// в <c>test-results/perf/</c>.
    ///
    /// Значения маркеров и счётчиков относятся к ПРЕДЫДУЩЕМУ кадру: профилировщик
    /// закрывает кадр раньше, чем начинается наш Update. Для поиска тормозов это неважно,
    /// но при сопоставлении с dt в CSV смещение на кадр надо держать в уме.</summary>
    public class PerfMonitor : MonoBehaviour
    {
        /// <summary>Длина окна: за столько кадров копится статистика, затем дамп в лог.</summary>
        private const int DumpEveryFrames = 120;

        /// <summary>Сколько кадров пытаться подключиться к маркеру, прежде чем сдаться.</summary>
        private const int AttachAttemptFrames = 600;

        /// <summary>Ёмкость CSV-буфера, кадров (~минута при 60 fps).</summary>
        private const int CsvCapacityFrames = 3600;

        /// <summary>Маркеры дешевле этого порога в дамп не попадают — только шум.</summary>
        private const float DumpThresholdMs = 0.05f;

        private const int TopMarkersInDump = 14;

        private static readonly float[] HistogramEdgesMs = { 8f, 16f, 33f, 50f };
        private static readonly string[] HistogramLabels = { "<8", "8-16", "16-33", "33-50", ">50" };

        public static PerfMonitor? Instance { get; private set; }
        public static bool Enabled { get; set; }

        /// <summary>Готовая строка для HUD, перестраивается 4 раза в секунду.</summary>
        public string HudText { get; private set; } = "";

        private struct MarkerSlot
        {
            public string Name;
            public ProfilerRecorder Recorder;
            public float LastMs;
            public float SumMs;
            public float MaxMs;
            public int Samples;
        }

        private MarkerSlot[]? _slots;

        // Счётчики движка
        private ProfilerRecorder _gcFrame;
        private ProfilerRecorder _gcUsed;
        private ProfilerRecorder _mainThread;
        private ProfilerRecorder _drawCalls;
        private ProfilerRecorder _batches;
        private ProfilerRecorder _setPass;

        // Окно
        private int _windowFrames;
        private float _dtSumMs, _dtMaxMs;
        private readonly int[] _histogram = new int[HistogramLabels.Length];
        private double _gcSumBytes;
        private float _gcMaxBytes;
        private int _getAllSum;

        // Худший кадр окна
        private int _worstFrame;
        private float _worstDtMs;
        private float _worstGcBytes;
        private float[]? _worstMarkersMs;

        // Подключение маркеров
        private int _attachFrames;
        private bool _attachReported;

        // CSV и HUD
        private PerfCsvLog? _csv;
        private float[]? _row;
        private float _nextHudTime;

        private const int CsvFixedColumns = 9;

        private void Awake()
        {
            Instance = this;

            // Обращение к Names прогревает PerfMarkers целиком: все маркеры создаются
            // до StartNew, поэтому рекордеры валидны сразу, а не «когда-нибудь потом».
            var names = PerfMarkers.Names;

            _slots = new MarkerSlot[names.Count];
            for (int i = 0; i < names.Count; i++)
            {
                _slots[i] = new MarkerSlot { Name = names[i] };
                TryAttach(ref _slots[i]);
            }

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

        private void Update()
        {
            HandleHotkeys();
            if (!Enabled) return;

            Sample();
            if (++_windowFrames >= DumpEveryFrames) Dump();
        }

        private void HandleHotkeys()
        {
            if (!Input.GetKeyDown(KeyCode.F9)) return;

            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (shift) ToggleCsv();
            else
            {
                Enabled = !Enabled;
                if (Enabled) StartWindow();
                Debug.Log($"[Perf] замер {(Enabled ? "включён" : "выключен")}");
            }
        }

        private void ToggleCsv()
        {
            if (_csv == null) return;

            if (_csv.Recording)
            {
                int rows = _csv.Rows;
                var path = _csv.Stop();
                Debug.Log(path != null
                    ? $"[Perf] CSV: {rows} кадров → {path}"
                    : "[Perf] CSV: писать нечего");
                return;
            }

            // Запись без замера бессмысленна — включаем заодно.
            if (!Enabled)
            {
                Enabled = true;
                StartWindow();
            }
            _csv.Start();
            Debug.Log($"[Perf] запись CSV пошла (до {CsvCapacityFrames} кадров, Shift+F9 — стоп)");
        }

        // ── Сбор ────────────────────────────────────────────────────────────

        private void Sample()
        {
            if (_slots == null) return;

            bool attaching = _attachFrames < AttachAttemptFrames;
            if (attaching) _attachFrames++;

            float maxMs = 0f;
            for (int i = 0; i < _slots.Length; i++)
            {
                ref var s = ref _slots[i];
                if (!s.Recorder.Valid)
                {
                    if (attaching) TryAttach(ref s);
                    if (!s.Recorder.Valid) { s.LastMs = 0f; continue; }
                }

                // Ноль — законный семпл: маркер, который срабатывает не каждый кадр,
                // должен давать честное среднее ПО КАДРАМ, а не по срабатываниям.
                float ms = s.Recorder.LastValue * 1e-6f;
                s.LastMs = ms;
                s.SumMs += ms;
                if (ms > s.MaxMs) s.MaxMs = ms;
                s.Samples++;
                if (ms > maxMs) maxMs = ms;
            }

            if (!attaching) ReportUnattachedOnce();

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
                    for (int i = 0; i < _slots.Length; i++) _worstMarkersMs[i] = _slots[i].LastMs;
            }

            WriteCsvRow(dtMs, gcBytes, getAll);

            if (Time.unscaledTime >= _nextHudTime)
            {
                _nextHudTime = Time.unscaledTime + 0.25f;
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
            for (int i = 0; i < _slots.Length; i++) _row[CsvFixedColumns + i] = _slots[i].LastMs;

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

        // ── Вывод ───────────────────────────────────────────────────────────

        private void Dump()
        {
            if (_slots == null || _windowFrames == 0) return;

            var sb = new StringBuilder();
            // Псевдографика U+2500 в литералах запрещена: её нет в атласе LiberationSans
            // (см. DebugLogSymbolTests), а лог показывается ещё и в ConsoleOverlay.
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

            AppendWorstFrame(sb);
            AppendWindowTop(sb);

            Debug.Log(sb.ToString());
            StartWindow();
        }

        /// <summary>Разбор самого долгого кадра окна — среднее спайк размывает, а рывок
        /// делает именно он.</summary>
        private void AppendWorstFrame(StringBuilder sb)
        {
            if (_slots == null || _worstMarkersMs == null || _worstFrame == 0) return;

            sb.AppendLine($"  худший кадр #{_worstFrame}: dt={_worstDtMs:F1}ms  gc={_worstGcBytes / 1024f:F1}КБ");
            var order = SortedByValue(_worstMarkersMs);
            int shown = 0;
            for (int k = 0; k < order.Length && shown < TopMarkersInDump; k++)
            {
                int i = order[k];
                if (_worstMarkersMs[i] < DumpThresholdMs) break;
                sb.AppendLine($"     {_slots[i].Name,-42} {_worstMarkersMs[i],6:F2}ms");
                shown++;
            }
            if (shown == 0) sb.AppendLine($"     все маркеры дешевле {DumpThresholdMs:F2}ms — время уходит мимо них");
        }

        private void AppendWindowTop(StringBuilder sb)
        {
            if (_slots == null) return;

            var avg = new float[_slots.Length];
            for (int i = 0; i < _slots.Length; i++)
                avg[i] = _slots[i].Samples > 0 ? _slots[i].SumMs / _slots[i].Samples : 0f;

            sb.AppendLine("  за окно (avg / max):");
            var order = SortedByValue(avg);
            int shown = 0;
            for (int k = 0; k < order.Length && shown < TopMarkersInDump; k++)
            {
                int i = order[k];
                if (_slots[i].Samples == 0)
                {
                    sb.AppendLine($"     {_slots[i].Name,-42} нет данных (маркер не подключён)");
                    shown++;
                    continue;
                }
                if (avg[i] < DumpThresholdMs && _slots[i].MaxMs < DumpThresholdMs) continue;
                sb.AppendLine($"     {_slots[i].Name,-42} {avg[i],6:F2}ms / {_slots[i].MaxMs,6:F2}ms");
                shown++;
            }
        }

        /// <summary>Индексы по убыванию значения. Вызывается раз в окно, поэтому
        /// вставками — короче и без аллокаций компаратора.</summary>
        private static int[] SortedByValue(float[] values)
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

            var last = new float[_slots.Length];
            for (int i = 0; i < _slots.Length; i++) last[i] = _slots[i].LastMs;
            var order = SortedByValue(last);
            for (int k = 0; k < 3 && k < order.Length; k++)
            {
                int i = order[k];
                if (last[i] < DumpThresholdMs) break;
                sb.AppendLine($"{_slots[i].Name,-40} {last[i],6:F2}ms");
            }

            if (_csv != null && _csv.Recording) sb.AppendLine($"● запись CSV: {_csv.Rows} кадров");
            return sb.ToString();
        }

        /// <summary>Печатает накопленное окно немедленно, не дожидаясь <see cref="DumpEveryFrames"/>.</summary>
        public static void DumpNow()
        {
            if (Instance != null) Instance.Dump();
        }

        // ── Служебное ───────────────────────────────────────────────────────

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

            if (_slots == null) return;
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i].SumMs = 0f;
                _slots[i].MaxMs = 0f;
                _slots[i].Samples = 0;
            }
        }

        private static void TryAttach(ref MarkerSlot s)
        {
            var r = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, s.Name);
            if (r.Valid) s.Recorder = r;
            else r.Dispose();
        }

        /// <summary>Маркер, к которому так и не удалось подключиться, иначе выглядел бы
        /// как «этот метод ничего не стоит». Сообщаем один раз.</summary>
        private void ReportUnattachedOnce()
        {
            if (_attachReported || _slots == null) return;
            _attachReported = true;

            var sb = new StringBuilder();
            for (int i = 0; i < _slots.Length; i++)
                if (!_slots[i].Recorder.Valid)
                    sb.Append(sb.Length == 0 ? "" : ", ").Append(_slots[i].Name);

            if (sb.Length > 0)
                Debug.LogWarning($"[Perf] маркеры не подключены: {sb}");
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

            if (_slots == null) return;
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].Recorder.Valid) _slots[i].Recorder.Dispose();
        }
    }
}

#endif
