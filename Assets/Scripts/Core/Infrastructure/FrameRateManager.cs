using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace KitchenDesigner.Core
{
    /// <summary>
    /// Динамический FPS: пока есть активность (ввод мыши/клавиатуры/касаний или
    /// идёт анимация двери/ящика) — держим активный FPS; в простое роняем до
    /// «почти нуля» (idle FPS), экономя CPU/батарею.
    ///
    /// Мгновенное пробуждение из простоя на WebGL обеспечивает FrameRateWake.jslib:
    /// он ловит события браузера и синхронно зовёт <see cref="OnBrowserActivity"/>,
    /// поэтому первый клик/движение после простоя не «залипает». На остальных
    /// платформах JS-хука нет, поэтому idle FPS там выше (пробуждение по опросу ввода).
    /// </summary>
    public class FrameRateManager : MonoBehaviour
    {
        public static FrameRateManager Instance { get; private set; }

        /// <summary>FPS при активности. WebGL — 30 (как было), иначе без ограничения.</summary>
        public int ActiveFps = DefaultActiveFps();

        /// <summary>FPS в простое. WebGL-плеер ~1 (есть мгновенное пробуждение),
        /// иначе умереннее (~10), т.к. пробуждение только по опросу ввода.</summary>
        public int IdleFps = DefaultIdleFps();

        /// <summary>Сколько ещё держать активный FPS после последней активности.</summary>
        public float IdleGraceSeconds = 0.7f;

        private float _wakeUntil;
        private Vector3 _lastMousePos;
        private int _appliedFps = int.MinValue;

        private static int DefaultActiveFps()
        {
#if UNITY_WEBGL
            return 30;
#else
            return -1; // платформенный дефолт (обычно vsync)
#endif
        }

        private static int DefaultIdleFps()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return 1;   // мгновенное пробуждение через JS-хук
#else
            return 10;  // без хука — пробуждение ~100 мс по опросу ввода
#endif
        }

        // ── Чистая логика «окна бодрствования» (тестируется без Unity-времени) ──

        /// <summary>Отметить активность в момент <paramref name="now"/>: держать
        /// активный FPS ещё <paramref name="graceSeconds"/> секунд.</summary>
        public void MarkActive(float now, float graceSeconds)
        {
            float until = now + Mathf.Max(0f, graceSeconds);
            if (until > _wakeUntil) _wakeUntil = until;
        }

        public bool IsAwake(float now) => now < _wakeUntil;

        public int TargetFpsAt(float now) => IsAwake(now) ? ActiveFps : IdleFps;

        // ── Публичное API для остального кода ──────────────────────────────

        /// <summary>Не давать заснуть ещё <paramref name="seconds"/> секунд
        /// (анимации, MCP-команды агента и т.п.). Безопасно, если менеджера нет.</summary>
        public static void KeepAwake(float seconds)
        {
            if (Instance != null) Instance.WakeFor(seconds);
        }

        private void WakeFor(float seconds)
        {
            MarkActive(Time.unscaledTime, seconds);
            Apply(ActiveFps); // поднять FPS сразу, не дожидаясь следующего Update
        }

        /// <summary>Вызывается из FrameRateWake.jslib при активности в браузере.</summary>
        public void OnBrowserActivity() => WakeFor(IdleGraceSeconds);

        // ── Жизненный цикл ─────────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;
            _lastMousePos = Input.mousePosition;
        }

        private void OnEnable()
        {
            MarkActive(Time.unscaledTime, IdleGraceSeconds);
            Apply(ActiveFps);
#if UNITY_WEBGL && !UNITY_EDITOR
            FrameRateWake_Init(gameObject.name);
#endif
        }

        private void Update()
        {
            if (DetectInput())
                MarkActive(Time.unscaledTime, IdleGraceSeconds);
            Apply(TargetFpsAt(Time.unscaledTime));
        }

        private bool DetectInput()
        {
            if (Input.anyKey) return true;            // включает кнопки мыши
            if (Input.touchCount > 0) return true;
            if (Input.mouseScrollDelta.sqrMagnitude > 0f) return true;
            var m = Input.mousePosition;
            if ((m - _lastMousePos).sqrMagnitude > 0f)
            {
                _lastMousePos = m;
                return true;
            }
            return false;
        }

        private void Apply(int fps)
        {
            if (fps == _appliedFps) return;
            _appliedFps = fps;
            Application.targetFrameRate = fps;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void FrameRateWake_Init(string gameObjectName);
#endif
    }
}
