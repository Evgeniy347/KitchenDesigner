using UnityEngine;

namespace KitchenDesigner.Core
{
    public class FacadeElement : KitchenElement
    {
        public int GapLeft
        {
            get => Data.GapLeft;
            set { Data.GapLeft = value; ApplyDimensions(); }
        }

        public int GapRight
        {
            get => Data.GapRight;
            set { Data.GapRight = value; ApplyDimensions(); }
        }

        public int GapTop
        {
            get => Data.GapTop;
            set { Data.GapTop = value; ApplyDimensions(); }
        }

        public int GapBottom
        {
            get => Data.GapBottom;
            set { Data.GapBottom = value; ApplyDimensions(); }
        }

        public int GapMM => Data.GapMM;

        protected override Vector3 EffectiveScale
        {
            get
            {
                var physical = transform.localScale;
                var gapX = (Data.GapLeft + Data.GapRight) * AppConstants.MM_TO_UNITS;
                var gapY = (Data.GapTop + Data.GapBottom) * AppConstants.MM_TO_UNITS;
                return physical + new Vector3(gapX, gapY, 0);
            }
        }

        // ── Открывание (дверца) ─────────────────────────────────────────
        // Дверца поворачивается вокруг выбранного ребра на 90° и обратно, с
        // плавностью по синусу (см. FacadeDoor). Позиция/поворот трансформа
        // вычисляются из ЗАКРЫТОЙ позы каждый кадр — без накопления ошибки.
        private const float OpenSeconds = 0.4f;

        [SerializeField] private DoorMode _mode = DoorMode.HingeFrontLeft;
        private bool _open;                              // целевое состояние
        private float _t;                                // прогресс 0..1 (линейный по времени)
        private Vector3 _closedPos;
        private Quaternion _closedRot = Quaternion.identity;

        /// <summary>Режим открывания (4 ребра или ящик). Смена на лету
        /// перерисовывает уже открытый фасад.</summary>
        public DoorMode Mode
        {
            get => _mode;
            set { _mode = value; if (_t > 0f) ApplyDoor(); }
        }

        /// <summary>Переключить режим по кругу (для кнопки-переключателя).</summary>
        public void CycleMode() => Mode = FacadeDoor.Next(_mode);

        public bool IsOpen => _open;
        public float DoorProgress => _t;
        public bool IsDoorClosed => !_open && _t <= 0f;

        public void ToggleDoor() => SetOpen(!_open);

        public void SetOpen(bool open)
        {
            if (open && _t <= 0f) CaptureClosed();
            _open = open;
        }

        /// <summary>Мгновенно вернуть закрытую позу (перед правкой размеров/позиции/поворота).</summary>
        public void ForceClose()
        {
            if (_t <= 0f && !_open) return;
            _open = false;
            _t = 0f;
            transform.SetPositionAndRotation(_closedPos, _closedRot);
        }

        private void CaptureClosed()
        {
            _closedPos = transform.position;
            _closedRot = transform.rotation;
        }

        private void Update() => StepDoor(Time.deltaTime);

        /// <summary>Один шаг анимации. Вынесен из Update, т.к. Update не зовётся
        /// в EditMode-тестах — так поведение двери можно проверять напрямую.</summary>
        public void StepDoor(float dt)
        {
            float target = _open ? 1f : 0f;
            if (Mathf.Approximately(_t, target))
            {
                // Закрыта и в покое → база следует за реальным трансформом
                // (чтобы перетаскивание/поворот закрытой дверцы обновляли базу).
                if (_t <= 0f) CaptureClosed();
                return;
            }
            float step = OpenSeconds > 0f ? dt / OpenSeconds : 1f;
            _t = Mathf.MoveTowards(_t, target, step);
            ApplyDoor();
        }

        private void ApplyDoor()
        {
            var half = transform.localScale * 0.5f;
            FacadeDoor.Pose(_closedPos, _closedRot, half, _mode, _t, out var pos, out var rot);
            transform.SetPositionAndRotation(pos, rot);
        }
    }
}
