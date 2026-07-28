#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace KitchenDesigner.Core
{
    /// <summary>Периодический дамп стоимости hot-path маркеров в консоль. Только dev-сборки.
    /// Выключен по умолчанию — включается F9 или <see cref="Enabled"/>, иначе дампы засоряют
    /// лог PlayMode-тестов, которые тоже поднимают Bootstrap.</summary>
    public class PerfMonitor : MonoBehaviour
    {
        /// <summary>Кадров в окне измерения: за окно копятся avg/min/max, затем дамп.</summary>
        private const int DumpEveryFrames = 120;

        /// <summary>Сколько кадров пытаться подключиться к маркеру, прежде чем сдаться.</summary>
        private const int AttachAttemptFrames = 600;

        public static bool Enabled { get; set; }

        private struct MarkerSlot
        {
            public string Name;
            public ProfilerRecorder Recorder;
            public float LastMs;
            public float SumMs;
            public float MinMs;
            public float MaxMs;
            public int Samples;
        }

        private MarkerSlot[]? _slots;
        private int _windowFrames;
        private int _attachFrames;
        private bool _attachReported;

        private void Awake()
        {
            var names = new[]
            {
                "WallManager.LateUpdate",
                "SceneVisibilityManager.Apply",
                "CameraController.Update",
                "CameraController.UpdateFloorVisibility",
                "FrameRateManager.Update",
                "FrameRateManager.DetectInput",
                "PartRegistry.GetAll",
                "ElementOutline.LateUpdate",
            };

            _slots = new MarkerSlot[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                _slots[i] = new MarkerSlot { Name = names[i] };
                ResetWindow(ref _slots[i]);
                TryAttach(ref _slots[i]);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F9))
            {
                Enabled = !Enabled;
                Debug.Log($"[Perf] {(Enabled ? "включён" : "выключен")}");
                if (Enabled) StartWindow();
            }

            if (!Enabled) return;

            Sample();
            if (++_windowFrames < DumpEveryFrames) return;
            Dump();
        }

        /// <summary>Читает маркеры за прошедший кадр. Ноль — законный семпл: маркер, который
        /// сработал не в каждом кадре, должен давать честное среднее по кадрам.</summary>
        private void Sample()
        {
            if (_slots == null) return;

            bool attaching = _attachFrames < AttachAttemptFrames;
            if (attaching) _attachFrames++;

            for (int i = 0; i < _slots.Length; i++)
            {
                ref var s = ref _slots[i];

                // Маркер регистрируется статическим инициализатором своего класса, то есть
                // при первом обращении к нему. На момент Awake его ещё может не быть —
                // подключаемся, пока не выйдет.
                if (!s.Recorder.Valid)
                {
                    if (attaching) TryAttach(ref s);
                    if (!s.Recorder.Valid) continue;
                }

                float ms = s.Recorder.LastValue * 1e-6f;
                s.LastMs = ms;
                s.SumMs += ms;
                if (ms < s.MinMs) s.MinMs = ms;
                if (ms > s.MaxMs) s.MaxMs = ms;
                s.Samples++;
            }

            if (!attaching) ReportUnattachedOnce();
        }

        private void Dump()
        {
            if (_slots == null) return;

            var sb = new StringBuilder();
            sb.AppendLine($"[Perf] -- frame {Time.frameCount}, окно {_windowFrames} кадров, dt={Time.deltaTime * 1000f:F1}ms --");

            for (int i = 0; i < _slots.Length; i++)
            {
                ref var s = ref _slots[i];
                if (s.Samples == 0)
                {
                    sb.AppendLine($"  {s.Name,-42} нет данных (маркер не подключён)");
                    continue;
                }

                float avg = s.SumMs / s.Samples;
                sb.AppendLine(s.MaxMs > 1f
                    ? $"  {s.Name,-42} cur={s.LastMs,6:F2}ms  avg={avg,6:F2}ms  min={s.MinMs,6:F2}ms  max={s.MaxMs,6:F2}ms  [!]"
                    : $"  {s.Name,-42} cur={s.LastMs,6:F2}ms  avg={avg,6:F2}ms  min={s.MinMs,6:F2}ms  max={s.MaxMs,6:F2}ms");
            }

            Debug.Log(sb.ToString());
            StartWindow();
        }

        /// <summary>Печатает накопленное окно немедленно, не дожидаясь <see cref="DumpEveryFrames"/>.</summary>
        public static void DumpNow()
        {
            var inst = FindAnyObjectByType<PerfMonitor>();
            if (inst != null) inst.Dump();
        }

        private void StartWindow()
        {
            _windowFrames = 0;
            if (_slots == null) return;
            for (int i = 0; i < _slots.Length; i++)
                ResetWindow(ref _slots[i]);
        }

        private static void ResetWindow(ref MarkerSlot s)
        {
            s.SumMs = 0f;
            s.MinMs = float.MaxValue;
            s.MaxMs = 0f;
            s.Samples = 0;
        }

        private static void TryAttach(ref MarkerSlot s)
        {
            var r = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, s.Name);
            if (r.Valid) s.Recorder = r;
            else r.Dispose();
        }

        /// <summary>Маркер, к которому так и не удалось подключиться, иначе выглядел бы как
        /// «этот метод ничего не стоит». Сообщаем один раз.</summary>
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
            if (_slots == null) return;
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].Recorder.Valid) _slots[i].Recorder.Dispose();
        }
    }
}

#endif
