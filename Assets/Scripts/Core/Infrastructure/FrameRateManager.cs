using UnityEngine;

namespace KitchenDesigner.Core
{
    public class FrameRateManager : MonoBehaviour
    {
        public static FrameRateManager? Instance { get; private set; }

        public int ActiveFps = DefaultActiveFps();

        public int IdleFps = DefaultIdleFps();

        public float IdleGraceSeconds = 0.7f;

        private float _wakeUntil;
        private Vector3 _lastMousePos;
        private int _appliedFps = int.MinValue;

        private static int DefaultActiveFps()
        {
            return -1;
        }

        private static int DefaultIdleFps()
        {
            return 10;
        }

        public void MarkActive(float now, float graceSeconds)
        {
            float until = now + Mathf.Max(0f, graceSeconds);
            if (until > _wakeUntil) _wakeUntil = until;
        }

        public bool IsAwake(float now) => now < _wakeUntil;

        public int TargetFpsAt(float now) => IsAwake(now) ? ActiveFps : IdleFps;

        public static void KeepAwake(float seconds)
        {
            if (Instance != null) Instance.WakeFor(seconds);
        }

        private void WakeFor(float seconds)
        {
            MarkActive(Time.unscaledTime, seconds);
            Apply(ActiveFps);
        }

        private void Awake()
        {
            Instance = this;
            _lastMousePos = Input.mousePosition;
        }

        private void OnEnable()
        {
            MarkActive(Time.unscaledTime, IdleGraceSeconds);
            Apply(ActiveFps);
        }

        private void Update()
        {
            using var _ = PerfMarkers.FrameRateUpdate.Auto();
            if (DetectInput())
                MarkActive(Time.unscaledTime, IdleGraceSeconds);
            Apply(TargetFpsAt(Time.unscaledTime));
        }

        private bool DetectInput()
        {
            using var _ = PerfMarkers.FrameRateDetectInput.Auto();
            if (Input.anyKey) return true;
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

    }
}
